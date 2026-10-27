using System.Linq.Expressions;
using Hub.Domain.Identity;

namespace Hub.Domain.Knowledge;

public static class DocumentAccessPolicy
{
    public static Expression<Func<Document, bool>> For(HubUser user) => d =>
        user.Role == UserRoles.Administrator || d.OwnerId == user.Id || d.Visibility == DocumentVisibility.Organization ||
        (d.Visibility == DocumentVisibility.Team && d.Department == user.Department);

    public static Expression<Func<Chunk, bool>> ForChunks(HubUser user) => c =>
        user.Role == UserRoles.Administrator || c.Document.OwnerId == user.Id || c.Document.Visibility == DocumentVisibility.Organization ||
        (c.Document.Visibility == DocumentVisibility.Team && c.Document.Department == user.Department);

    public static bool CanManage(Document document, HubUser user) =>
        user.Role == UserRoles.Administrator || document.OwnerId == user.Id;
}
