using System.Security.Cryptography;
using System.Text;
using System;
using System.Linq;

public static class GuidX
{
    public static Guid CreateGuidV3(string input)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] namespaceBytes = Guid.Empty.ToByteArray();

        using (var md5 = MD5.Create())
        {
            byte[] hash = md5.ComputeHash(namespaceBytes.Concat(inputBytes).ToArray());

            hash[6] = (byte)((hash[6] & 0x0F) | 0x30);
            hash[8] = (byte)((hash[8] & 0x3F) | 0x80);

            return new Guid(hash.Take(16).ToArray());
        }
    }
}
