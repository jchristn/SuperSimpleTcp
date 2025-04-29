
namespace SuperSimpleTcp
{
    /// <summary>
    /// PermitType's used to clarify the way to validate permitted IPs
    /// </summary>
    public enum PermitType : byte
    {
        /// <summary>
        /// Only PermittedList is used when only the PermittedIPs list decides
        /// </summary>
        OnlyPermittedList = 0,

        /// <summary>
        /// FlagAndPermittedList is used to check the flag AllowAnonymousIPs first
        /// and if the flag was false then checks the PermittedIPs list
        /// </summary>
        FlagAndPermittedList = 1
    }
}