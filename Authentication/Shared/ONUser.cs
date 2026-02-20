using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace IT.WebServices.Authentication
{
    public class ONUser : ClaimsPrincipal
    {
        public Guid Id { get; set; } = Guid.Empty;
        public const string IdType = "Id";

        public string UserName { get; set; } = "";
        public const string UserNameType = "sub";

        public string DisplayName { get; set; } = "";
        public const string DisplayNameType = "Display";

        public uint SubscriptionLevel { get; set; } = 0;
        public const string SubscriptionLevelType = "SubscriptionLevel";

        public string SubscriptionProvider { get; set; }
        public const string SubscriptionProviderType = "SubscriptionProvider";

        public List<string> Idents { get; private set; } = new List<string>();
        public const string IdentsType = "Idents";

        public readonly RoleAbilities RoleAbilities = new RoleAbilities();
        public List<string> Roles
        {
            get
            {
                return RoleAbilities.Roles;
            }

            private set
            {
                RoleAbilities.Roles.Clear();
                RoleAbilities.Roles.AddRange(value);
            }
        }
        public const string RolesType = ClaimTypes.Role;

        public List<Claim> ExtraClaims { get; private set; } = new List<Claim>();

        public string JwtToken { get; set; } = "";

        public bool IsLoggedIn => Id != Guid.Empty;

        public IEnumerable<Claim> ToClaims()
        {
            if (Id != Guid.Empty)
                yield return new Claim(IdType, Id.ToString());

            if (!string.IsNullOrWhiteSpace(UserName))
                yield return new Claim(UserNameType, UserName);

            if (!string.IsNullOrWhiteSpace(DisplayName))
                yield return new Claim(DisplayNameType, DisplayName);

            if (Idents.Count != 0)
                yield return new Claim(IdentsType, string.Join(';', Idents));

            foreach (var r in Roles)
                yield return new Claim(RolesType, r);

            foreach (var c in ExtraClaims)
                yield return c;
        }

        public static ONUser Parse(Claim[] claims)
        {
            if (claims == null || claims.Length == 0)
                return null;

            var user = new ONUser();

            foreach (var claim in claims)
                user.LoadClaim(claim);

            if (!user.IsValid())
                return null;

            return user;
        }

        public override bool IsInRole(string role) => Roles.Contains(role);

        private bool IsValid()
        {
            return true; // Id != Guid.Empty;
        }

        private void LoadClaim(Claim claim)
        {
            switch (claim.Type)
            {
                case IdType:
                    Id = Guid.Parse(claim.Value);
                    return;
                case UserNameType:
                    UserName = claim.Value;
                    return;
                case DisplayNameType:
                    DisplayName = claim.Value;
                    return;
                case IdentsType:
                    Idents.AddRange(claim.Value.Split(';'));
                    return;
                case RolesType:
                    Roles.Add(claim.Value);
                    return;
                case SubscriptionLevelType:
                    if (uint.TryParse(claim.Value, out uint i))
                        SubscriptionLevel = i;
                    return;
                case SubscriptionProviderType:
                    SubscriptionProvider = claim.Value;
                    return;
                default:
                    ExtraClaims.Add(claim);
                    return;
            }
        }
    }
}
