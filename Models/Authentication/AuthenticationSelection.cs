using Fido2NetLib.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Models.Authentication
{
    public class AuthenticationSelection
    {
        public UserVerificationRequirement UserVerification { get; set; }
        public AuthenticatorAttachment? AuthenticatorAttachment { get; set; }
        public ResidentKeyRequirement? ResidentKey { get; set; }
    }
}
