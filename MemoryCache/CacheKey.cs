using System;

namespace Zettai
{
    internal class CacheKey
    {
        public bool Valid { get; }
        public Guid Id { get; }
        public long Version { get; }
        public string IdString { get; }
        public string VersionString { get; }
        public CacheKey(string id, string fileId)
        {
            bool IdParseSuccess = long.TryParse(fileId, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out long parsed);
            bool GuidParseSuccess = Guid.TryParse(id, out var guid);
            Valid = IdParseSuccess && GuidParseSuccess;
            if (Valid)
            {
                Version = parsed;
                Id = guid;
                return;
            }
            IdString = id;
            VersionString = fileId;
        }
        public CacheKey(Guid id, long fileId)
        {
            Version = fileId;
            Id = id;
            Valid = true;
        }
        public override bool Equals(object obj)
        {
            if (obj is CacheKey key)
                return (Valid && key.Valid && Version == key.Version && Id == key.Id) 
                    || (!Valid && !key.Valid && string.Equals(VersionString, key.VersionString) && string.Equals(IdString, key.IdString));
            else
                return base.Equals(obj);
        }
        public override string ToString() => Valid ? Id.ToString() + '_' + Version : '!' +  IdString + '_' + VersionString;
        public override int GetHashCode() => (int)(Id.GetHashCode() * Version);
    }
}
