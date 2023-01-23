using System;

namespace Zettai
{
    internal class CacheKey
    {
        public bool Valid { get; }
        public Guid Id { get; }
        public long Version { get; }
        public ulong KeyHash { get; }
        public string IdString { get; }
        public string VersionString { get; }
        public CacheKey(string id, string fileId, ulong keyHash)
        {
            long.TryParse(fileId, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out long parsed);
            bool GuidParseSuccess = Guid.TryParse(id, out var guid);
            Valid = GuidParseSuccess;
            KeyHash = keyHash;
            if (Valid)
            {
                Version = parsed;
                Id = guid;
                return;
            }
            IdString = id;
            VersionString = fileId;
        }
        public CacheKey(Guid id, long fileId, ulong keyHash)
        {
            Version = fileId;
            Id = id;
            Valid = true;
            IdString = string.Empty;
            VersionString = string.Empty;
            KeyHash = keyHash;
        }
        public override bool Equals(object obj)
        {
            if (obj is CacheKey key)
                return (Valid && key.Valid && KeyHash == key.KeyHash && Id == key.Id) 
                    || (!Valid && !key.Valid && string.Equals(VersionString, key.VersionString) && string.Equals(IdString, key.IdString));
            else
                return base.Equals(obj);
        }
        public override string ToString() => Valid ? Id.ToString() + '_' + KeyHash : '!' +  IdString + '_' + KeyHash;
        public override int GetHashCode() => (int)(Id.GetHashCode() * (long)KeyHash);
    }
}
