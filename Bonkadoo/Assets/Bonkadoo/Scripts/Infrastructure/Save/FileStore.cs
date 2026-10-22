using System;
using System.Collections.Generic;
using System.IO;

namespace Bonkadoo.Infrastructure.Save
{
    /// <summary>Minimal file primitives the save layer needs; swappable for fault-injection tests.</summary>
    public interface IFileStore
    {
        bool Exists(string name);
        byte[] Read(string name);
        void Write(string name, byte[] data);
        /// <summary>Atomically moves <paramref name="source"/> over <paramref name="destination"/>.</summary>
        void Replace(string source, string destination);
        void Delete(string name);
    }

    public sealed class DiskFileStore : IFileStore
    {
        private readonly string _root;

        public DiskFileStore(string root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            Directory.CreateDirectory(_root);
        }

        private string P(string name) => Path.Combine(_root, name);

        public bool Exists(string name) => File.Exists(P(name));
        public byte[] Read(string name) => File.ReadAllBytes(P(name));

        public void Write(string name, byte[] data)
        {
            using (var fs = new FileStream(P(name), FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.Write(data, 0, data.Length);
                fs.Flush(true);
            }
        }

        public void Replace(string source, string destination)
        {
            var src = P(source);
            var dst = P(destination);
            if (File.Exists(dst)) File.Replace(src, dst, null);
            else File.Move(src, dst);
        }

        public void Delete(string name)
        {
            if (File.Exists(P(name))) File.Delete(P(name));
        }
    }

    public sealed class InMemoryFileStore : IFileStore
    {
        private readonly Dictionary<string, byte[]> _files = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        /// <summary>Test hook: throw after writing this many bytes of the next Write (simulates power loss).</summary>
        public int? FailNextWriteAfterBytes { get; set; }
        public bool FailNextReplace { get; set; }

        public IReadOnlyDictionary<string, byte[]> Files => _files;

        public bool Exists(string name) => _files.ContainsKey(name);

        public byte[] Read(string name) =>
            _files.TryGetValue(name, out var d) ? (byte[])d.Clone() : throw new FileNotFoundException(name);

        public void Write(string name, byte[] data)
        {
            if (FailNextWriteAfterBytes.HasValue)
            {
                var n = Math.Min(FailNextWriteAfterBytes.Value, data.Length);
                FailNextWriteAfterBytes = null;
                var partial = new byte[n];
                Array.Copy(data, partial, n);
                _files[name] = partial;
                throw new IOException("Simulated interrupted write");
            }
            _files[name] = (byte[])data.Clone();
        }

        public void Replace(string source, string destination)
        {
            if (FailNextReplace)
            {
                FailNextReplace = false;
                throw new IOException("Simulated interrupted replace");
            }
            _files[destination] = _files[source];
            _files.Remove(source);
        }

        public void Delete(string name) => _files.Remove(name);

        public void Corrupt(string name, int offset, byte value) => _files[name][offset] = value;
    }
}
