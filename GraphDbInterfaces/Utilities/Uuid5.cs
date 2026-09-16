using System;
using System.Security.Cryptography;
using System.Text;


namespace GraphRoots.GraphDb
{
    public static class Uuid5
    {
        public static Guid Generate(Guid namespaceId, string name)
        {
            // Convert name to bytes
            byte[] nameBytes = Encoding.UTF8.GetBytes(name);

            return Generate(namespaceId, nameBytes);
        }

        public static Guid Generate(Guid namespaceId, byte[] nameBytes)
        {
            // Convert namespace UUID to bytes (big-endian)
            byte[] namespaceBytes = namespaceId.ToByteArray();
            SwapByteOrder(namespaceBytes);

            // Concatenate namespace + name and hash with SHA1
            byte[] hash;
            using (SHA1 sha1 = SHA1.Create())
            {
                sha1.TransformBlock(namespaceBytes, 0, namespaceBytes.Length, null, 0);
                sha1.TransformFinalBlock(nameBytes, 0, nameBytes.Length);
                hash = sha1.Hash ?? throw new InvalidOperationException("SHA1 hash was not produced.");
            }

            // Take first 16 bytes of SHA1 hash
            byte[] newGuid = new byte[16];
            Array.Copy(hash, 0, newGuid, 0, 16);

            // Set version (5) and variant bits
            newGuid[6] = (byte)((newGuid[6] & 0x0F) | (5 << 4)); // version 5
            newGuid[8] = (byte)((newGuid[8] & 0x3F) | 0x80);     // variant RFC 4122

            // Convert back to little-endian for Guid constructor
            SwapByteOrder(newGuid);
            return new Guid(newGuid);
        }

        private static void SwapByteOrder(byte[] guid)
        {
            void Swap(int a, int b)
            {
                byte tmp = guid[a];
                guid[a] = guid[b];
                guid[b] = tmp;
            }

            // Swap to match RFC 4122 network byte order
            Swap(0, 3); Swap(1, 2);
            Swap(4, 5); Swap(6, 7);
        }
    }

}
