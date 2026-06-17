using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace AgricHub.Shared.DTO_s.Response
{
    public class AuthenticationResponse
    {
        public JwtToken? JwtToken { get; set; }
        public string? UserType { get; set; }
        public string? FullName { get; set; }
        public bool TwoFactor { get; set; }
        public bool IsExisting { get; set; }
        public bool NeedsRoleSelection { get; set; }  // ← NEW
    }

    public class JwtToken
    {
        public string Token { get; set; }
        public DateTime Issued { get; set; }
        public DateTime? Expires { get; set; }
    }
}

