using Shared.Core.Interfaces;

namespace Shared.Core.Extensions
{
    public static class AuditExtensions
    {
        public static T ApplyAudit<T>(this T entity, string userId, bool isNew) where T : IAuditable
        {
            var now = DateTime.UtcNow;
            if (isNew)
            {
                entity.CreatedBy = userId;
                entity.CreatedDate = now;
            }
            else
            {
                entity.UpdatedBy = userId;
                entity.UpdatedDate = now;
            }
            return entity;  // 👈 Ahora retorna la entidad modificada
        }
    }
}
