using Microsoft.AspNetCore.Identity;

namespace Anvil.Store;

public sealed class StoreUser : IdentityUser
{
    public string? DisplayName { get; set; }
    public bool IsCreator { get; set; }
}
