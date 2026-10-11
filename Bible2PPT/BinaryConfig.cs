using System.IO;

namespace Bible2PPT
{
    abstract class BinaryConfig
    {
        protected string path;
        protected int size;

        public BinaryConfig(string path, int size)
        {
            this.path = path;
            this.size = size;
            Reload();
        }

        public void Save()
        {
            using var fs = File.Create(path);
            fs.Write(Serialize(), 0, size);
        }

        public void Reload()
        {
            try
            {
                using var fs = File.OpenRead(path);
                if (fs.Length != size) return;
                var data = new byte[size];
                var offset = 0;
                while (offset < size)
                {
                    var count = fs.Read(data, offset, size - offset);
                    if (count == 0) return;
                    offset += count;
                }
                Deserialize(data);
            }
            catch (FileNotFoundException)
            {
                // noop
            }
        }

        protected abstract byte[] Serialize();
        protected abstract void Deserialize(byte[] s);
    }
}
