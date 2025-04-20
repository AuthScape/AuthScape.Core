using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Models.Authentication
{
    public enum ResidentKeyRequirement
    {
        /// <summary>
        /// The authenticator SHOULD NOT create a client-side discoverable credential
        /// </summary>
        Discouraged,

        /// <summary>
        /// The authenticator can create either type of credential
        /// </summary>
        Preferred,

        /// <summary>
        /// The authenticator MUST create a client-side discoverable credential
        /// </summary>
        Required
    }
}
