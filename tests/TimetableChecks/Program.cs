using System.IO.Compression;
using System.Xml.Linq;
using SchoolMessenger.Contracts;
using SchoolMessenger.Shared;
using Microsoft.Data.Sqlite;

var directory=args.Length>0?Path.GetFullPath(args[0]):Path.Combine(Path.GetTempPath(),"yy-timetable-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
var checks=0;void Check(bool condition,string name){if(!condition)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
void Rejected(Action action,string name){try{action();}catch(FilePolicyException){Check(true,name);return;}throw new Exception("Accepted invalid input: "+name);}
string Id()=>Guid.NewGuid().ToString("N");
string Address(int row,int column){var label="";while(column>0){column--;label=(char)('A'+column%26)+label;column/=26;}return label+row;}
void Fixture(string name,bool daily=false,bool empty=false,int classes=6,bool formula=false,bool external=false,bool wrongDay=false)
{
    XNamespace ns="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    var cells=new List<XElement>();var merges=new List<string>();
    void Cell(int r,int c,string value)=>cells.Add(new XElement(ns+"c",new XAttribute("r",Address(r,c)),new XAttribute("t","inlineStr"),new XElement(ns+"is",new XElement(ns+"t",value))));
    Cell(3,2,daily?"2026 학년도 2 학기 1 학년":"2026 년 2학기 시간표");
    if(!daily)
    {
        var column=4;foreach(var (day,index) in new[]{"월","화","수","목","금","토","일"}.Select((d,i)=>(d,i)))
        {
            Cell(5,column,day);var start=column;
            for(var p=1;p<=12;p++)
            {
                Cell(6,column,p+".0");
                for(var room=1;room<=classes;room++)Cell(6+room,column,index<5&&p<=(index is 1 or 2?7:6)?"수학(가상 교사 A)":"");
                if(index==3&&p==5){for(var row=6;row<=6+classes;row++)merges.Add(Address(row,column)+":"+Address(row,column+1));column++;}
                column++;
            }
            if(column!=start+12&&index!=3)throw new Exception("Fixture width");
        }
        for(var room=1;room<=classes;room++)Cell(6+room,3,$"{room}(1학년 {room}반)");
    }
    else
    {
        Cell(2,17,"2026.10.07.");Cell(6,2,"일자");Cell(6,3,"교시");var columns=new[]{4,5,6,8,10,14};
        for(var room=1;room<=classes;room++){Cell(7,columns[room-1],$"1학년 {room}반");if(room>2)merges.Add(Address(7,columns[room-1])+":"+Address(7,columns[room-1]+(room==5?3:1)));}
        Cell(8,2,"2026.10.08.\n("+(wrongDay?"수":"목")+"요일)");merges.Add("B8:B19");
        for(var p=1;p<=12;p++)
        {
            Cell(7+p,3,p+".0");for(var room=1;room<=classes;room++)
            {var changed=room==2&&p==2||room==4&&p is 4 or 5;Cell(7+p,columns[room-1],empty||p>6?"":changed?"음악(가상 교사 B)":"수학(가상 교사 A)");if(room>2)merges.Add(Address(7+p,columns[room-1])+":"+Address(7+p,columns[room-1]+(room==5?3:1)));}
        }
    }
    if(formula)cells[0].Add(new XElement(ns+"f","HYPERLINK(\"https://invalid.test\")"));
    using var zip=ZipFile.Open(Path.Combine(directory,name),ZipArchiveMode.Create);
    void Write(string path,XDocument xml){using var output=zip.CreateEntry(path).Open();xml.Save(output);}
    XNamespace rel="http://schemas.openxmlformats.org/officeDocument/2006/relationships",pkg="http://schemas.openxmlformats.org/package/2006/relationships";
    Write("[Content_Types].xml",new(new XElement("Types")));
    Write("xl/workbook.xml",new(new XElement(ns+"workbook",new XElement(ns+"sheets",new XElement(ns+"sheet",new XAttribute("name","sheet1"),new XAttribute(rel+"id","rId1"))))));
    Write("xl/_rels/workbook.xml.rels",new(new XElement(pkg+"Relationships",new XElement(pkg+"Relationship",new XAttribute("Id","rId1"),new XAttribute("Target","worksheets/sheet1.xml"),external?new XAttribute("TargetMode","External"):null))));
    Write("xl/worksheets/sheet1.xml",new(new XElement(ns+"worksheet",new XElement(ns+"sheetData",cells.GroupBy(c=>new string(((string)c.Attribute("r")!).Where(char.IsDigit).ToArray())).Select(g=>new XElement(ns+"row",g))),new XElement(ns+"mergeCells",merges.Select(m=>new XElement(ns+"mergeCell",new XAttribute("ref",m)))))));
}
foreach(var file in new[]{"base.xlsx","daily.xlsx","empty.xlsx","partial.xlsx","formula.xlsx","external.xlsx","wrong-day.xlsx"})File.Delete(Path.Combine(directory,file));
Fixture("base.xlsx");Fixture("daily.xlsx",true);Fixture("empty.xlsx",true,true);Fixture("partial.xlsx",true,false,1);Fixture("formula.xlsx",formula:true);Fixture("external.xlsx",external:true);Fixture("wrong-day.xlsx",true,wrongDay:true);
var baseline=TimetableWorkbook.Read(Path.Combine(directory,"base.xlsx"),"base");var daily=TimetableWorkbook.Read(Path.Combine(directory,"daily.xlsx"),"daily");
Check(baseline.Cells.Count(c=>c.Subject.Length>0)==192,"baseline reads 192 lessons and merged Thursday period 5");
Check(daily.Date=="2026-10-08"&&daily.Cells.Count(c=>c.Subject.Length>0)==36,"dated timetable uses lesson date rather than export date");
Check(TimetableWorkbook.Read(Path.Combine(directory,"empty.xlsx"),"daily").Cells.All(c=>c.Subject==""),"entire empty dated day cancels lessons");
Rejected(()=>TimetableWorkbook.Read(Path.Combine(directory,"base.xlsx"),"daily"),"wrong workbook kind rejected");
Rejected(()=>TimetableWorkbook.Read(Path.Combine(directory,"formula.xlsx"),"base"),"formulas rejected");
Rejected(()=>TimetableWorkbook.Read(Path.Combine(directory,"external.xlsx"),"base"),"external relationships rejected");
Rejected(()=>TimetableWorkbook.Read(Path.Combine(directory,"wrong-day.xlsx"),"daily"),"inconsistent date and weekday rejected");
var rooms=baseline.Cells.Select(c=>c.ClassKey).Distinct().ToDictionary(c=>c,_=>Id());var teachers=new Dictionary<string,string>{{"가상 교사 A",Id()},{"가상 교사 B",Id()}};var owner=Id();
TimetableBatch Batch(TimetableWorkbookData source,long rev)=>new(Id(),rev,owner,Id(),source.Kind,2026,2,source.Date??"2026-08-01",source.Date??"2027-02-28",source.Date,"fixture.xlsx",rev,false,source.Cells.Select(c=>new TimetableCell(rooms[c.ClassKey],c.ClassKey,c.Day,c.Period,c.Subject,c.Teacher.Length>0?teachers[c.Teacher]:"",c.Teacher)).ToArray());
var b=Batch(baseline,1);var d=Batch(daily,2);TimetableData.Validate(b);TimetableData.Validate(d);
var ids=rooms.Values.ToArray();var final=TimetableData.Resolve([b,d],"2026-10-08",ids);
Check(final.Slots.Count(s=>s.Changed)==3,"three dated changes compared against baseline");
Check(final.Slots.Count(s=>s.Subject.Length>0)==36,"empty dated periods remain empty");
Check(TimetableData.Resolve([b,d],"2026-10-09",ids).Slots.All(s=>s.Source=="base"),"no daily file falls back to baseline");
var partial=Batch(TimetableWorkbook.Read(Path.Combine(directory,"partial.xlsx"),"daily"),3);
Check(TimetableData.Resolve([b,partial],"2026-10-08",ids).Slots.Count(s=>s.Source=="daily")==12,"partial daily file overrides only included class");
Check(TimetableData.Resolve([b,d,d with{Id=Id(),Revision=3,Withdrawn=true}],"2026-10-08",ids).Slots.All(s=>s.Source=="base"),"cancelling dated publication restores baseline");
Check(TimetableData.Resolve([b,d,d with{Id=Id(),Revision=3},d with{Id=Id(),Revision=4,Withdrawn=true}],"2026-10-08",ids).Slots.All(s=>s.Source=="base"),"cancellation does not resurrect previous daily version");
Check(TimetableData.Resolve([b],"2026-07-31",ids).MissingClasses.Length==6,"outside semester reports missing timetable");
Rejected(()=>TimetableData.Validate(d with{Cells=[d.Cells[0],d.Cells[0]]}),"duplicate normalized lessons rejected");
Rejected(()=>TimetableData.Validate(d with{Cells=[null!]}),"null lesson rejected without server error");
Rejected(()=>TimetableData.Validate(d with{Cells=[d.Cells[0] with{Day=1}]}),"bridge weekday mismatch rejected");
var dbPath=Path.Combine(directory,"unit-"+Id()+".db");SqliteConnection Open(){var db=new SqliteConnection("Data Source="+dbPath);db.Open();return db;}var data=new TimetableData(Open);data.Initialize();
var saved=data.Insert(b,[new(0,owner,b.Start,ids,"등록",1)]);Check(saved.Revision==1&&data.Notices(owner).Length==1,"schedule and durable notices saved together");
Check(data.ByClient(owner,b.ClientId)?.Id==b.Id,"publication retry lookup survives persisted state");
var replicaPath=Path.Combine(directory,"replica-"+Id()+".db");var replica=new TimetableData(()=>{var db=new SqliteConnection("Data Source="+replicaPath);db.Open();return db;});replica.Initialize();
var delivery=new TimetableDelivery(saved,[],data.BatchNotices(1));replica.Replicate(delivery);replica.Replicate(delivery);Check(replica.Revision==1&&replica.Notices(owner).Length==1,"repeated bridge delivery is idempotent");
Rejected(()=>replica.Replicate(delivery with{Batch=saved with{FileName="tampered.xlsx"}}),"bridge cannot rewrite a published revision");
if(args.Length==3)
{
    var realBase=TimetableWorkbook.Read(args[1],"base");var realDaily=TimetableWorkbook.Read(args[2],"daily");
    Check(realBase.Cells.Count(c=>c.Subject.Length>0)==192&&realDaily.Cells.Count(c=>c.Subject.Length>0)==36&&realDaily.Date=="2026-10-08","provided school files match inspected layout and date");
    var changed=realDaily.Cells.Count(c=>{var before=realBase.Cells.SingleOrDefault(b=>b.ClassKey==c.ClassKey&&b.Day==c.Day&&b.Period==c.Period);return c.Subject!=(before?.Subject??"")||c.Teacher!=(before?.Teacher??"");});Check(changed==3,"provided files resolve exactly three changed lessons without storing school names in tests");
}
Console.WriteLine($"Timetable parser and resolver: {checks} checks passed.");
