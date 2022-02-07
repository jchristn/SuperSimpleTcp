using System;
using System.Linq;

namespace SimpleTcp.UnitTest
{
    internal static class StringHelper
    {
        private static Random _random = new Random();
        private static readonly string _chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        public static string RandomString(int length)
        {          
            return new string(Enumerable.Repeat(_chars, length).Select(s => s[_random.Next(s.Length)]).ToArray());
        }
    }
}
