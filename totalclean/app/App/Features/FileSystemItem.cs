using IOCore.Files;
using IOCore.Utils;
using System;
using System.Collections.Generic;
using System.IO;

namespace IOApp.Features
{
    public partial class FileSystemItem : FileItem, IEquatable<FileSystemItem?>
    {
        string _key = "";
        public string Key { get => _key; private set => SetAndNotify(ref _key, value); }

        public readonly bool UseHashAsKey;

        bool _isDeleted;
        public bool IsDeleted { get => _isDeleted; set => SetAndNotify(ref _isDeleted, value); }

        FileSystemItem(string path, string key, bool useHashAsKey) : base(path)
        {
            Key = key;
            UseHashAsKey = useHashAsKey;
        }

        public static FileSystemItem Create(string path, bool useHashAsKey)
        {
            var key = path;

            if (useHashAsKey)
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096);
                key = CryptographyUtils.GetSHA256(fs);
            }

            return new(path, key, useHashAsKey);
        }

        public override void Refresh(string path, bool notify)
        {
            base.Refresh(path, notify);

            if (UseHashAsKey)
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096);
                _key = CryptographyUtils.GetSHA256(fs);
            }
            else
                _key = path;

            if (notify)
                Notify(null);
        }

        public void Delete(bool notify)
        {
            File.SetAttributes(InputInfo.FullName, FileAttributes.Normal);
            File.Delete(InputInfo.FullName);

            _isDeleted = true;

            if (notify)
                Notify(nameof(IsDeleted));
        }

        public override bool Equals(object? obj) => Equals(obj as FileSystemItem);

        public bool Equals(FileSystemItem? other)
        {
            if (other is null)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            return Key.Equals(other.Key, StringComparison.Ordinal) ||
                    InputInfo.FullName.Equals(other.InputInfo.FullName, StringComparison.InvariantCultureIgnoreCase);
        }

        public override int GetHashCode() => HashCode.Combine(Key.GetHashCode());

        public static bool operator ==(FileSystemItem? left, FileSystemItem? right) => EqualityComparer<FileSystemItem>.Default.Equals(left, right);

        public static bool operator !=(FileSystemItem? left, FileSystemItem? right) => !EqualityComparer<FileSystemItem>.Default.Equals(left, right);
    }
}