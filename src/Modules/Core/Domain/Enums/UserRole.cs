namespace Aidly.src.Modules.Core.Domain.Enums;

public enum UserRole
{
    Admin    = 1,   // Full access
    Manager  = 2,   // Branch-level access
    Cashier  = 3,   // POS only
    Viewer   = 4    // Read only
}