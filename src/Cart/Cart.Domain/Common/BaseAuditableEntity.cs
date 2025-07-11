﻿
using Shared.Core.Interfaces;

namespace Cart.Domain.Common
{
    public class BaseAuditableEntity : IAuditable
    {
        public Guid Id { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }
}
