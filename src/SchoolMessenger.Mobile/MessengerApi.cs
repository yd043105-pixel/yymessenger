using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SchoolMessenger.Contracts;

namespace SchoolMessenger.Mobile;

public sealed class MessengerApi : IDisposable
{
    readonly CookieContainer cookies=new();
    readonly HttpClient http;
    readonly string key;
    public bool Office{get;}
    public Uri Address{get;}
    public JsonElement Session{get;private set;}
    public bool Remember{get;set;}
    public event Action? Invalidated;
    string csrf="";
    public MessengerApi(string address,bool office)
    {
        if(!Uri.TryCreate(address.TrimEnd('/')+"/",UriKind.Absolute,out var uri)||uri.AbsolutePath!="/"||uri.UserInfo.Length>0||uri.Query.Length>0||uri.Fragment.Length>0)throw new InvalidOperationException("학교에서 안내한 서버 기본 주소를 입력하세요.");
        var localDebug=false;
#if DEBUG
        localDebug=uri.Scheme=="http"&&(uri.IsLoopback||uri.Host=="10.0.2.2");
#endif
        if(uri.Scheme!="https"&&!localDebug)throw new InvalidOperationException("서버 주소는 https://로 시작해야 합니다.");
        Address=uri;Office=office;key="session-"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes((office?"office:":"portal:")+uri.AbsoluteUri)));
        http=new HttpClient(new HttpClientHandler{CookieContainer=cookies,AllowAutoRedirect=false}){BaseAddress=uri,Timeout=TimeSpan.FromSeconds(90)};
    }
    public async Task Restore()
    {
        try{var data=await SecureStorage.Default.GetAsync(key);if(data is null)return;foreach(var cookie in JsonSerializer.Deserialize(data,MobileJson.Default.MobileCookieArray)??[])if(cookie.Expires>DateTime.UtcNow)cookies.Add(Address,new Cookie(cookie.Name,cookie.Value,"/"){Expires=cookie.Expires,Secure=Address.Scheme=="https",HttpOnly=true});Remember=true;}
        catch{SecureStorage.Default.Remove(key);}
    }
    async Task Persist()
    {
        if(!Remember){SecureStorage.Default.Remove(key);return;}
        var list=cookies.GetCookies(Address).Cast<Cookie>().Where(c=>!c.Expired).Select(c=>new MobileCookie(c.Name,c.Value,c.Expires==DateTime.MinValue?DateTime.UtcNow.AddDays(30):c.Expires)).ToArray();
        await SecureStorage.Default.SetAsync(key,JsonSerializer.Serialize(list,MobileJson.Default.MobileCookieArray));
    }
    public async Task Refresh()
    {
        var previouslyAuthenticated=Session.ValueKind==JsonValueKind.Object&&Session.GetProperty("user").ValueKind!=JsonValueKind.Null;
        Session=await Get("api/session");csrf=Session.GetProperty("csrfToken").GetString()!;
        if(Session.GetProperty("user").ValueKind==JsonValueKind.Null){SecureStorage.Default.Remove(key);if(previouslyAuthenticated)Invalidated?.Invoke();}
        else await Persist();
    }
    public async Task<JsonElement> Get(string path)
    {using var response=await http.GetAsync(path);await Check(response);return await Read(response);}
    public async Task<JsonElement> Send(string path,object? body=null,HttpContent? content=null)
    {
        if(csrf.Length==0)await Refresh();using var request=new HttpRequestMessage(HttpMethod.Post,path);request.Headers.Add("X-CSRF-TOKEN",csrf);var payload=body??new MobileEmpty();request.Content=content??JsonContent.Create(payload,MobileJson.Default.GetTypeInfo(payload.GetType())??throw new InvalidOperationException("지원하지 않는 요청입니다."));
        using var response=await http.SendAsync(request);await Check(response);var value=await Read(response);await Persist();return value;
    }
    public async Task Login(string username,string password,bool remember)
    {Remember=remember;await Refresh();await Send("api/login",new MobileLogin(username,password,remember));await Refresh();}
    public async Task Logout()
    {try{await Send("api/logout");}finally{SecureStorage.Default.Remove(key);foreach(Cookie c in cookies.GetCookies(Address))c.Expired=true;Session=default;Invalidated?.Invoke();}}
    public async Task<JsonElement> Upload(FileResult file,bool external)
    {using var content=new MultipartFormDataContent();if(!Office)content.Add(new StringContent(Guid.NewGuid().ToString("N")),"clientId");content.Add(new StreamContent(await file.OpenReadAsync()),"file",file.FileName);return await Send(external&&Office?"api/external-announcements/attachments":"api/attachments",content:content);}
    public async Task ShareFile(string id,string name)
    {
        using var response=await http.GetAsync("api/attachments/"+id+"/download",HttpCompletionOption.ResponseHeadersRead);await Check(response);
        if(response.Content.Headers.ContentLength>100L*1024*1024)throw new InvalidOperationException("파일이 너무 큽니다.");
        var folder=Path.Combine(FileSystem.CacheDirectory,"yy-share");Directory.CreateDirectory(folder);var path=Path.Combine(folder,Guid.NewGuid().ToString("N")+"-"+Path.GetFileName(name));
        try{await using(var output=File.Create(path))await response.Content.CopyToAsync(output);await Share.Default.RequestAsync(new ShareFileRequest("첨부 저장·공유",new ShareFile(path)));}
        catch{if(File.Exists(path))File.Delete(path);throw;}
        // Android returns when the chooser opens; deleting here would break the selected recipient's stream.
    }
    public static void CleanupShares()
    {
        var folder=Path.Combine(FileSystem.CacheDirectory,"yy-share");if(!Directory.Exists(folder))return;
        foreach(var file in Directory.EnumerateFiles(folder))if(File.GetLastWriteTimeUtc(file)<DateTime.UtcNow.AddDays(-1))File.Delete(file);
    }
    async Task Check(HttpResponseMessage response)
    {
        if(response.IsSuccessStatusCode)return;if(response.StatusCode==HttpStatusCode.Unauthorized){SecureStorage.Default.Remove(key);Session=default;Invalidated?.Invoke();}
        string message=(int)response.StatusCode switch{401=>"로그인이 만료되었습니다. 다시 로그인하세요.",403=>"승인된 계정과 담당 학급 권한을 확인하세요.",429=>"요청이 많습니다. 잠시 후 다시 시도하세요.",_=>"서버 연결·학교 접속 권한을 확인하세요."};
        try{var data=await response.Content.ReadFromJsonAsync(MobileJson.Default.JsonElement);if(data.TryGetProperty("error",out var error))message=error.GetString()??message;}catch(JsonException){}
        throw new InvalidOperationException(message);
    }
    static async Task<JsonElement> Read(HttpResponseMessage response){var text=await response.Content.ReadAsStringAsync();if(string.IsNullOrEmpty(text))return default;using var document=JsonDocument.Parse(text);return document.RootElement.Clone();}
    public void Dispose()=>http.Dispose();
}
internal record MobileCookie(string Name,string Value,DateTime Expires);
internal record MobileLogin(string Username,string Password,bool RememberLogin);
internal record MobileRegistration(string? Code,string? Username,string? Password);
internal record MobileEmpty;
[JsonSourceGenerationOptions(PropertyNamingPolicy=JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(MobileCookie[]))]
[JsonSerializable(typeof(MobileLogin))]
[JsonSerializable(typeof(MobileRegistration))]
[JsonSerializable(typeof(MobileEmpty))]
[JsonSerializable(typeof(AnnouncementRequest))]
[JsonSerializable(typeof(JsonElement))]
internal partial class MobileJson:JsonSerializerContext;
