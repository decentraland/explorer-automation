using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using AltTester.AltTesterSDK.Driver;
using NUnit.Framework;

public class PackageIdentityTests
{
    [Test]
    public void LoadedDriverMatchesVendoredPackageAndSourceCommit()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "alttester-provenance.json")));
        var root = manifest.RootElement;
        var package = Path.Combine(AppContext.BaseDirectory, root.GetProperty("package").GetString());
        string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        Assert.That(Hash(package), Is.EqualTo(root.GetProperty("sha256").GetString()));
        var assembly = typeof(AltDriver).Assembly;
        Assert.That(Hash(assembly.Location), Is.EqualTo(root.GetProperty("dll_sha256").GetProperty("lib/net5.0/AltDriver.dll").GetString()));
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
        Assert.That(version, Does.Contain(root.GetProperty("source_commit").GetString()));
        TestContext.Progress.WriteLine($"Loaded {assembly.FullName}; informational={version}; SHA256={Hash(assembly.Location)}");
    }
}
