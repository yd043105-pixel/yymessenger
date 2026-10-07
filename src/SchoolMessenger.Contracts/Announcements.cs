namespace SchoolMessenger.Contracts;

public record PortalClass(string Id, string Name, int Grade);
public record DirectoryPerson(string Id, string Role, string? ClassId, string? InternalUserId, bool CanBroadcast, string[] Classes, string[] Children, int ScopeVersion=1, Dictionary<string,string>? FamilyStamps=null);
public record PortalDirectory(PortalClass[] Classes, DirectoryPerson[] People);
public record TeacherProof(string Id, bool Active, bool CanBroadcast);
public record BridgeHeartbeat(TeacherProof[] Teachers);
public record AnnouncementRequest(string ClientId, string Title, string Body, string Audience, string[] ClassIds, bool All, string[] AttachmentIds);
public record AnnouncementTarget(string UserId, string StudentId, string ClassId, string Scope="");
public static class AnnouncementScopes
{
    public static string Stamp(DirectoryPerson student,DirectoryPerson? parent=null)=>student.ScopeVersion+":"+(parent is null?"student":parent.ScopeVersion+":"+parent.FamilyStamps?.GetValueOrDefault(student.Id));
}
public record BridgeAnnouncement(string TeacherId, AnnouncementRequest Request, AnnouncementTarget[] Targets);
public record PortalFile(string Id, string Name, long Size, long? ExpiresAt = null, bool Expired = false);
public record PortalNotice(string Id, string Title, string Body, string SenderName, long PublishedAt, long? ReadAt, bool Withdrawn, int RecipientCount, int ReadCount);
public record PortalReceipt(string UserId, string Name, string StudentName, long? ReadAt, bool Revoked, string ClassId);
public record PortalNoticeDetail(PortalNotice Notice, PortalFile[] Attachments, PortalReceipt[]? Receipts);
public record PortalUser(string Id, string Name, string Role, bool CanBroadcast, string[] Classes);
public record PortalSession(string SchoolName, string CsrfToken, PortalUser? User);
