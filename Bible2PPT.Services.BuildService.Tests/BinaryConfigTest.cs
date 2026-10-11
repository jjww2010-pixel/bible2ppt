using Xunit;

namespace Bible2PPT.Services.BuildService.Tests;

public sealed class BinaryConfigTest
{
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(5)]
    public void InvalidLengthKeepsDefaultsAndSaveWritesExactLength(int size)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, Enumerable.Repeat((byte)255, size).ToArray());
            var config = new SampleConfig(path);
            Assert.Equal(6, config.Value);
            config.Save();
            Assert.Equal(4, new FileInfo(path).Length);
            Assert.Equal(6, new SampleConfig(path).Value);
        }
        finally { File.Delete(path); }
    }

    private sealed class SampleConfig : BinaryConfig
    {
        public int Value { get; private set; } = 6;
        public SampleConfig(string path) : base(path, 4) { }
        protected override byte[] Serialize() => BitConverter.GetBytes(Value);
        protected override void Deserialize(byte[] bytes) => Value = BitConverter.ToInt32(bytes);
    }
}
