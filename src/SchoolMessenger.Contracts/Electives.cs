namespace SchoolMessenger.Contracts;

public record ElectiveCourse(string Id,string Name,string[] TeacherIds);
public record ElectiveGroup(string Id,string Name,int Required,ElectiveCourse[] Courses);
public record ElectiveRoundRequest(string ClientId,string Title,string Term,string[] ClassIds,long StartsAt,long EndsAt,ElectiveGroup[] Groups,int Revision=0);
public record ElectiveTransition(string ClientId,int Revision,string Action);
public record ElectiveApplicationRequest(string ClientId,int Revision,string[] CourseIds);
public record ElectiveCancellation(string ClientId,int Revision);
public record ElectiveApplication(int Revision,bool Submitted,string[] CourseIds,long? UpdatedAt);
public record ElectiveRound(string Id,string Title,string Term,string[] ClassIds,long StartsAt,long EndsAt,string State,int Revision,ElectiveGroup[] Groups,bool CanApply,ElectiveApplication? Application=null);
public record ElectiveStudent(string Id,string Name,string ClassId,string ClassName,bool Submitted,int Revision,string[] CourseIds,long? UpdatedAt);
public record ElectiveSection(string CourseId,string SectionId);
public record ElectiveReport(ElectiveRound Round,ElectiveStudent[] Students,ElectiveSection[] Sections,int ExcludedApplications);
public record ElectiveManagerChange(string UserId,bool Enabled);
