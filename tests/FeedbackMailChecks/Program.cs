using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using SchoolMessenger.Contracts;
using SchoolMessenger.Shared;

var root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);
var checks=new List<string>();
void Check(bool valid,string label){if(!valid)throw new InvalidOperationException(label);checks.Add(label);}
SqliteConnection Open(){var db=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=Path.Combine(root,"synthetic.db"),ForeignKeys=true}.ToString());db.Open();return db;}
void Sql(string text){using var db=Open();using var c=db.CreateCommand();c.CommandText=text;c.ExecuteNonQuery();}
long Value(string text){using var db=Open();using var c=db.CreateCommand();c.CommandText=text;return Convert.ToInt64(c.ExecuteScalar());}
using(var db=Open())using(var c=db.CreateCommand()){c.CommandText="CREATE TABLE Users(Id TEXT PRIMARY KEY,Name TEXT NOT NULL,Active INTEGER NOT NULL);CREATE TABLE FeedbackReports(Id TEXT PRIMARY KEY,OwnerId TEXT NOT NULL,Payload TEXT NOT NULL,CreatedAt INTEGER NOT NULL);INSERT INTO Users VALUES('synthetic-owner','김동욱',1)";c.ExecuteNonQuery();FeedbackMail.EnsureSchema(db);}
var protection=new EphemeralDataProtectionProvider();var protector=protection.CreateProtector("SchoolMessenger.Feedback.v1.office");
var queue=new FeedbackMail(Open,"office",protection,new ConfigurationBuilder().AddInMemoryCollection().Build());
var at=new DateTimeOffset(2026,10,15,5,5,0,TimeSpan.Zero).ToUnixTimeMilliseconds();
string Add(string? code=null){var id=Guid.NewGuid().ToString("N");var report=new FeedbackRequest(Guid.NewGuid().ToString("N"),"bug","windows","messages","PRIVATE TITLE",code is null?"PRIVATE MESSAGE password=secret":"POST /api/todos · TaskFeatures.Map · 줄 75","1.0.0",code);using var db=Open();using var c=db.CreateCommand();c.CommandText="INSERT INTO FeedbackReports VALUES($id,'synthetic-owner',$payload,$at);INSERT INTO FeedbackMail(ReportId) VALUES($id)";c.Parameters.AddWithValue("$id",id);c.Parameters.AddWithValue("$payload",protector.Protect(JsonSerializer.Serialize(report,new JsonSerializerOptions(JsonSerializerDefaults.Web))));c.Parameters.AddWithValue("$at",at);c.ExecuteNonQuery();return id;}
Check(FeedbackMail.Subject(at,"김동욱")=="[여양메신저 오류로그]10월15일14시05분 김동욱 오류","Korean time and requested subject format");
Check(!FeedbackMail.Subject(at,"김\r\nBcc:\u202e동욱").Any(c=>char.IsControl(c)||c=='\u202e'),"Control and direction characters cannot enter subject headers");
var delivered=new List<FeedbackEnvelope>();
Task Send(FeedbackEnvelope envelope,CancellationToken token){delivered.Add(envelope);return Task.CompletedTask;}
Check(!await queue.DeliverOne(Send),"Empty outbox does not send email");
var first=Add();await queue.DeliverOne(Send);await queue.DeliverOne(Send);
Check(delivered.Count==1&&Value("SELECT COUNT(*) FROM FeedbackMail WHERE SentAt IS NOT NULL")==1,"Delivered report is not sent again");
Check(delivered[0].Id=="office-"+first&&!delivered[0].Body.Contains("PRIVATE")&&!delivered[0].Body.Contains("secret")&&!delivered[0].Body.Contains("synthetic-owner"),"Email omits original title, description, password and account ID");
using(var message=FeedbackMail.CreateMessage(delivered[0],"sender@example.invalid","recipient@example.invalid"))Check(message.Headers["Message-ID"]=="<yy-feedback-office-"+first+"@school-messenger.invalid>"&&message.Subject==delivered[0].Subject&&message.SubjectEncoding?.WebName=="utf-8"&&!message.IsBodyHtml,"Actual mail message has stable identity, UTF-8 Korean subject and plain-text content");
var retry=Add();await queue.DeliverOne((_,_)=>throw new System.Net.Mail.SmtpException("PRIVATE AUTHENTICATION DETAILS"));
Check(Value("SELECT Attempts FROM FeedbackMail WHERE ReportId='"+retry+"'")==1&&Value("SELECT NextAttempt FROM FeedbackMail WHERE ReportId='"+retry+"'")>DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),"Failed delivery is retained with persistent retry backoff");
Check(!await queue.DeliverOne(Send),"Backoff prevents immediate repeated SMTP attempts");
Sql("UPDATE FeedbackMail SET NextAttempt=0 WHERE ReportId='"+retry+"'");await queue.DeliverOne(Send);
Check(delivered.Count==2&&delivered[1].Id=="office-"+retry,"Retry succeeds with a stable report identity");
var fail=Add();for(var n=0;n<8;n++){Sql("UPDATE FeedbackMail SET NextAttempt=0 WHERE ReportId='"+fail+"'");await queue.DeliverOne((_,_)=>throw new IOException("PRIVATE"));}
Check(!await queue.DeliverOne(Send)&&Value("SELECT COUNT(*) FROM FeedbackMail WHERE Attempts=8 AND LastError='delivery-failed'")==1,"Eight failed attempts stop automatic retries without storing sensitive errors");
var concurrent=Add();var started=new TaskCompletionSource();var release=new TaskCompletionSource();
var sending=queue.DeliverOne(async(envelope,token)=>{started.SetResult();await release.Task;delivered.Add(envelope);});await started.Task;
Check(!await queue.DeliverOne(Send),"Durable reservation prevents concurrent duplicate delivery");release.SetResult();await sending;
var erased=Add();Sql("DELETE FROM FeedbackReports WHERE Id='"+erased+"'");
Check(Value("SELECT COUNT(*) FROM FeedbackMail WHERE ReportId='"+erased+"'")==0,"Report deletion removes its pending email");
Add("HTTP_500");await queue.DeliverOne(Send);Check(delivered.Last().Body.Contains("TaskFeatures.Map")&&!delivered.Last().Body.Contains("PRIVATE"),"Only server-generated diagnostic metadata is included for automatic errors");
var legacy=new FeedbackRequest("client","bug","windows","messages","title","details","1.0.0");Check(!JsonSerializer.Serialize(legacy,new JsonSerializerOptions(JsonSerializerDefaults.Web)).Contains("errorCode"),"Absent diagnostic code preserves the original manual report replay payload");
Console.WriteLine(JsonSerializer.Serialize(new{checks},new JsonSerializerOptions{WriteIndented=true}));
