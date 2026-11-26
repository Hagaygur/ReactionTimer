using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

internal static class Program
{
    private const string ExpectedPluginId = "fc0b3f68-e30d-4d1f-b2ac-4e5d62bb1725";
    private const string ExpectedPluginName = "Alch Reaction Timer";
    private const string ExpectedCompany = "Warned";
    private const string ExpectedAssemblyBase = "VoK.ReactionTimer";
    private const string ExpectedIconResource = "Icon.png";

    private static readonly string[] ForbiddenAssemblies =
    {
        "System.Windows.Forms",
        "System.Windows.Forms.Primitives",
        "System.Media",
        "System.Windows.Extensions"
    };

    // DH 5 host security verifier constraints (mirrors DungeonHelper.dll 퉧.퉍)
    private static readonly HashSet<string> ForbiddenPInvokes = new(StringComparer.Ordinal)
    {
        "ReadProcessMemory", "WriteProcessMemory", "OpenProcess",
        "VirtualAllocEx", "VirtualProtectEx", "CreateRemoteThread",
        "NtReadVirtualMemory", "NtWriteVirtualMemory",
        "SetWindowsHookExA", "SetWindowsHookExW",
        "LoadLibraryA", "LoadLibraryW", "LoadLibraryExA", "LoadLibraryExW",
        "GetProcAddress"
    };

    private static readonly HashSet<string> ForbiddenSecurityTypes = new(StringComparer.Ordinal)
    {
        "System.Runtime.Loader.AssemblyLoadContext",
        "System.Runtime.InteropServices.NativeLibrary",
        "System.Reflection.Emit.DynamicMethod",
        "System.Reflection.Emit.ILGenerator",
        "System.Reflection.Emit.AssemblyBuilder",
        "System.Reflection.Emit.MethodBuilder",
        "System.Reflection.Emit.TypeBuilder"
    };

    private static readonly HashSet<(string TypeFullName, string MemberName)> ForbiddenSecurityMembers = new()
    {
        ("System.Runtime.InteropServices.Marshal", "GetDelegateForFunctionPointer"),
        ("System.Reflection.Assembly", "Load"),
        ("System.Reflection.Assembly", "LoadFile"),
        ("System.Reflection.Assembly", "LoadFrom"),
        ("System.Reflection.Assembly", "UnsafeLoadFrom")
    };

    private static readonly string[] ForbiddenArchiveFiles =
    {
        "VoK.Sdk.dll",
        "Newtonsoft.Json.dll",
        "log4net.dll",
        "System.Drawing.Common.dll"
    };

    // DH 5 Beta public issuer keys (from DungeonHelper.Common.Trust.IssuerKeys.All)
    private static readonly string[] Dh5IssuerPublicKeysPem =
    {
        """
        -----BEGIN PUBLIC KEY-----
        MIICIjANBgkqhkiG9w0BAQEFAAOCAg8AMIICCgKCAgEA/NSKszqyT4VuQ2Wqdv7m
        a/3uzuiWAksb4a4YdGpGY51ERz/nLq6/d9OkPSWgd/EFtuAbrthlTMacX4LvBgW1
        tjMgUnzoa/rV1kz1uYF/vUXGwmaQIdlcGwJ3etM509/0y0PrMYmy4bN6OGo2sFdl
        ERtzKOKIun3c9duqYfSyTCeWZycKPLNbHsMO2eCPc50Gt2clN7h4Tl8kseT9I0cw
        7nNJBtAPOKhAG9CeiPCM6nDaK/h3CVhTy/ATZpScwbI1B8PoU5NwTs2rciM/AHSI
        x98US3nO0A5/rFValMn1WAYEme0PGe9UGY06B1fc9H36c4BmSIvGhmbURLLn2dqI
        0Iv8BpKpqo0rARi2x+8JbIgp46DutuL7GBfluLTGRmAAT+qujlLSgmb2BS95iaxU
        /E6JGpIBtxFsLAJBq1EEuVECz6s6pYwtU4X0Xc+0/SkOB9K959P9ggjx+oTvkzYY
        +pZct8QJ29z36TEkJo2pRNMT5KATrKU//rVvHm68rmmVztp58RcVFVJBe0MjQE85
        WimZMhzimOCpBi8Pe145n9nV3EOeC5wRzX0EE19bEYhpIBOkQnXkw8pp+NJaQmRi
        g+btOCM2BnMlJGvb4zqtqwNfBFrYp75iDNOcF9KY4qYh1OuBZ6i2sckQ4zFpdwda
        Mh0xljf1LIvwH0E14M/zXCcCAwEAAQ==
        -----END PUBLIC KEY-----
        """,
        """
        -----BEGIN PUBLIC KEY-----
        MIICIjANBgkqhkiG9w0BAQEFAAOCAg8AMIICCgKCAgEA7NRxqo5aL2dvgiA8EHO7
        ebwFWFmng3XxwkKyDqROB23bQZ8fB+MQgp83r5GRQ+wMG+BAXzfIsVnbXRbQu2xr
        Of0jph3f7VYk/bUIXqumCRgQvpKMNkzTFG8tST3kodIib0oL/RyLxbxZHULy7rLx
        NXMpKPaDprLMdPX4yab3pXVUuG6+Ku10BmJ5ZpEYpTRJdlX3cqWv113BBKh/cmqU
        KEh31Td5bJSTopnTu54I5Yblihr47DgqXCg67krz4v1DxmsZjdU7eREBNvu8u7RE
        HZ7D6FHNXUtAPwVIqUZwrk09rPggWFBjP0/kyY/OcfjEEGRCaPKPFpEit0N+4eRe
        wzXP5cJfZnQMN9JBA0QSqx8jx46MbYRgQAHHxgEYtoGWECG6+bLDPR5rYyuy8vR6
        m2E8S5h9e7NPdA8s0wpo2r4lG25aHzTD2i99gwYpBaiqzXeBMZMz3GEGn8Lfx1fZ
        l+jjKKXrbLPrPhhxi6WgIx7TpeVllReLF8aK1lVWhF+e35VB4lBnFbfTCJXjdtdE
        JQCmESR3yOuCCrckPtyutIZDpTJJEAb5X2cgazDOYiyClZR7xa9BhP1Qtr/rmPFc
        inkikyNrtgzP6hRgD9EsvNmXhrQoSsXJV9oQxhpleCVbDB6E5ZZWc/THZHTm0jjf
        XfR9M5d/QeDiu0A75hOwG/cCAwEAAQ==
        -----END PUBLIC KEY-----
        """,
        """
        -----BEGIN PUBLIC KEY-----
        MIICIjANBgkqhkiG9w0BAQEFAAOCAg8AMIICCgKCAgEArHeOe3w5hbuuW2uJUDXx
        8ks9q7GeWRqW56+C84TS8CUFrS7YkI9LUmkyl5ezb6CqWOVmZuuus+X+loKJb6z6
        EPs/19g7Kyoxhhnt0YJ5UOfBiPW9xcR0hUYs7zS3JncFqayHs67QZAvxPg7Qu3QK
        X5w8dWYXvGDYyYc7q0+QKid4AuS3ihGKpIJGBn/gpYcZR8+yLQ+iTP3gZrzeTjuM
        BrFMWCB4NazkBl6Q6b5HyhxDhJTVONfDcLfKg/Vw/Rz9x6Xh22D8rCfyvfR8jlDk
        4VcXS3U25PaYrRBkD6Ucju8LUiO32sZJSXLGhn7BMoRG34nDvrMVlUGyHfAbUQpD
        zHG4+E7tip7Bkq9bHuJvmfFJBrPx+0qrml8ZFlEOuLCuRDJIcgmXa+BznBWgyFLl
        vkJYzrmqExQcySN4ckbXHEeiv/3SpTIqGVWuG/hqhdYdYQvwPv8XY8aGzbAs/8kM
        Evq7Ey3jioSqRB+WakesXeC8TpFM5sNbQxtS3aLo7ScZhNj4Npl2UEzs7tibczmZ
        5UqSUTDH9Qx1cLvVD7LN2DuN7knibwaH42rHU/jfOfg/ZjTRFNqhxvwqDOEI0ynb
        tK1cCjiotJVzuLbIYQFn742Q7KAU96UqQ1ziFYXCpZYBxX2xOeu/6G33VU2M9216
        tnVLJz9H+7Bva0hYkDNjDMMCAwEAAQ==
        -----END PUBLIC KEY-----
        """,
        """
        -----BEGIN PUBLIC KEY-----
        MIICIjANBgkqhkiG9w0BAQEFAAOCAg8AMIICCgKCAgEAsxBADWLHQH/j38K79WFi
        3HQlyU73gF9iM433UewsK3zoRXMpsRg83n4Ts34TtTne/MobuCWpK78imw/DEZF8
        RZD8zBwe2uTg7whqBST/fX1xc8xofv4NwxXvMm4DivnNsZq1Zk3GiBGxxKjIqntk
        Fw1U1ekpFOdBBuGi+UousOqVDdObAcKdDX1srCUqXOV3uYKNx2btSbVUfXY4Yaha
        Ynnct/aEGWlLQushMfrWx5vihY/150KI36k9RV4SeTdfsfANnAOrn5k/jTUpRMNi
        zyaTCz7LyyaPbeDAOQxCEz/ZL6Csszc4nClvqOaOejKuMlSJ2xbfj0U3GKb8EcRt
        sLC4/XmHcaZ/5i7H02NvGXPribBI/bb7sYssoYpyYuk/Pp4+J8NxiRqjbUCfdGOU
        jUUnnIFQQdiOpu23X2Mp2Dy7DT8e65+tB043k2tTKwbHfHk18vCfTbN+rDjBIPEu
        YurlaAgje3CN4EJrIXt6nE+skeStkVkB7GSbYUbiyZP6hITtPfVUR3LoMLO4NUYx
        tBgvQ13bTdL2J8kMove79BOPZTii9qfzfbmoiM2y6G+QEvHgZylVjrQQ1pwehSbt
        Mw0wrVxFKUO8FFvd2xz4VrzsuRRMXTvSSNGTAHnZIhY1DeWcdXv4HpCwkvPLQoLE
        rftxfNSLLIyzuHvQ1DFmxxECAwEAAQ==
        -----END PUBLIC KEY-----
        """
    };

    private static int _checks;

    private static void Pass(string message)
    {
        ++_checks;
        Console.WriteLine("PASS " + message);
    }

    private static void Fail(string message) => throw new Exception("FAILED: " + message);

    private static void Require(bool condition, string message)
    {
        if (!condition) Fail(message);
        Pass(message);
    }

    private static async Task<int> Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: Dh5Smoke <plugin.zip|VoK.ReactionTimer.dll> <dh5-sdk-dir> [--require-signed]");
            return 2;
        }

        string targetPath = Path.GetFullPath(args[0]);
        string sdkDir = Path.GetFullPath(args[1]);
        bool requireSigned = args.Skip(2).Any(a => a.Equals("--require-signed", StringComparison.OrdinalIgnoreCase));
        string sdkDllPath = Path.Combine(sdkDir, "VoK.Sdk.dll");
        if (!File.Exists(targetPath)) Fail("Target not found: " + targetPath);
        if (!File.Exists(sdkDllPath)) Fail("DH 5 VoK.Sdk.dll not found: " + sdkDllPath);

        string tempDir = Path.Combine(Path.GetTempPath(), "dh5-smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string dllPath;
            if (targetPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                dllPath = VerifyArchiveAndTrust(targetPath, tempDir, requireSigned);
            }
            else
            {
                if (requireSigned) Fail("--require-signed requires a packaged DH 5 .zip archive.");
                dllPath = targetPath;
                Pass("Running binary and lifecycle verification directly on DLL (unsigned mode)");
            }

            VerifyPeTokensAndReferences(dllPath, sdkDllPath);
            await VerifyIsolatedLoadAndBehaviorAsync(dllPath, sdkDllPath, tempDir);
            Console.WriteLine($"Dh5Smoke: {_checks} checks passed.");
            return 0;
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private static string VerifyArchiveAndTrust(string zipPath, string extractDir, bool requireSigned)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var entries = archive.Entries.ToList();
        Require(entries.Count > 0, "DH 5 archive is non-empty");

        var byName = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            string name = entry.FullName;
            if (name.StartsWith("plugins/", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("./", StringComparison.Ordinal) ||
                name.StartsWith("/", StringComparison.Ordinal) ||
                name.Contains('\\') ||
                name.Contains("..", StringComparison.Ordinal))
                Fail("Non-canonical or prefixed archive entry: " + name);

            if (name.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".key", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".pfx", StringComparison.OrdinalIgnoreCase))
                Fail("Forbidden debug/secret artifact in archive: " + name);

            if (ForbiddenArchiveFiles.Contains(Path.GetFileName(name), StringComparer.OrdinalIgnoreCase))
                Fail("Host-provided DLL leaked into archive: " + name);

            using var ms = new MemoryStream();
            using (var s = entry.Open()) s.CopyTo(ms);
            byName[name] = ms.ToArray();
        }
        Pass("Archive root layout canonical with zero leaked host/secret/PDB files");

        Require(byName.ContainsKey($"{ExpectedAssemblyBase}.dll"), $"Archive contains {ExpectedAssemblyBase}.dll at root");
        Require(byName.ContainsKey($"{ExpectedAssemblyBase}.deps.json"), $"Archive contains {ExpectedAssemblyBase}.deps.json at root");
        Require(byName.ContainsKey("plugin.metadata"), "Archive contains plugin.metadata at root");

        using (var metaDoc = JsonDocument.Parse(byName["plugin.metadata"]))
        {
            var root = metaDoc.RootElement;
            Require(root.GetProperty("PluginName").GetString() == ExpectedPluginName, "plugin.metadata PluginName matches");
            string? curVer = root.GetProperty("CurrentVersion").GetString();
            Require(!string.IsNullOrWhiteSpace(curVer), "plugin.metadata CurrentVersion present");
            var allowed = root.GetProperty("AllowedVersions");
            Require(allowed.ValueKind == JsonValueKind.Array && allowed.GetArrayLength() >= 1,
                "plugin.metadata AllowedVersions non-empty");
        }

        string entName = $"{ExpectedPluginName}.entitlement";
        string entSigName = $"{ExpectedPluginName}.entitlement.sig";
        string pemName = $"{ExpectedPluginName}.pem";
        string manName = $"{ExpectedPluginName}.manifest";
        string manSigName = $"{ExpectedPluginName}.manifest.sig";

        bool hasSignedArtifacts = byName.ContainsKey(entName) || byName.ContainsKey(manName) || byName.ContainsKey(pemName);
        if (requireSigned && !hasSignedArtifacts)
            Fail("Archive is unsigned, but --require-signed was specified");

        // Extract DLL (and supporting files) for Authenticode + ALC loading
        foreach (var kv in byName)
        {
            string dest = Path.Combine(extractDir, kv.Key);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.WriteAllBytes(dest, kv.Value);
        }
        string extractedDll = Path.Combine(extractDir, $"{ExpectedAssemblyBase}.dll");

        if (hasSignedArtifacts)
        {
            Require(byName.ContainsKey(entName) && byName.ContainsKey(entSigName), "Entitlement and signature present");
            Require(byName.ContainsKey(pemName), "Author public PEM present");
            Require(byName.ContainsKey(manName) && byName.ContainsKey(manSigName), "Manifest and signature present");

            // 1. Verify entitlement against DH 5 public IssuerKeys
            bool entVerified = false;
            foreach (string issuerPem in Dh5IssuerPublicKeysPem)
            {
                using var issuerRsa = RSA.Create();
                issuerRsa.ImportFromPem(issuerPem);
                if (issuerRsa.VerifyData(byName[entName], byName[entSigName], HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                {
                    entVerified = true;
                    break;
                }
            }
            Require(entVerified, "Entitlement signature verified against DH 5 IssuerKeys");

            string authorFp;
            using (var entDoc = JsonDocument.Parse(byName[entName]))
            {
                var root = entDoc.RootElement;
                Require(string.Equals(root.GetProperty("pluginId").GetString(), ExpectedPluginId, StringComparison.OrdinalIgnoreCase),
                    "Entitlement pluginId matches");
                Require(root.GetProperty("pluginName").GetString() == ExpectedPluginName,
                    "Entitlement pluginName matches");
                authorFp = root.GetProperty("authorKeyFingerprint").GetString() ?? "";
                Require(authorFp.Length == 64, "Entitlement authorKeyFingerprint is 64-char SHA-256 hex");
            }

            // 2. Verify .pem SPKI SHA-256 == authorKeyFingerprint
            using var authorRsa = RSA.Create();
            authorRsa.ImportFromPem(Encoding.UTF8.GetString(byName[pemName]));
            string pemFp = Convert.ToHexString(SHA256.HashData(authorRsa.ExportSubjectPublicKeyInfo()));
            Require(string.Equals(pemFp, authorFp, StringComparison.OrdinalIgnoreCase),
                "Author PEM SPKI SHA-256 matches entitlement authorKeyFingerprint");

            // 3. Verify manifest signature & bidirectional SHA-256 match of all other files
            bool manSigOk = authorRsa.VerifyData(byName[manName], byName[manSigName], HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            Require(manSigOk, "Manifest signature verified against author PEM");

            using (var manDoc = JsonDocument.Parse(byName[manName]))
            {
                var root = manDoc.RootElement;
                Require(root.GetProperty("formatVersion").GetInt32() == 1, "Manifest formatVersion is 1");
                Require(root.GetProperty("pluginName").GetString() == ExpectedPluginName, "Manifest pluginName matches");
                var manifestMap = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var item in root.GetProperty("entries").EnumerateArray())
                {
                    string p = item.GetProperty("path").GetString()!;
                    string h = item.GetProperty("sha256").GetString()!;
                    manifestMap[p] = h;
                }
                var expectedFiles = byName.Keys
                    .Where(k => k != manName && k != manSigName)
                    .OrderBy(k => k, StringComparer.Ordinal)
                    .ToList();
                Require(manifestMap.Count == expectedFiles.Count && expectedFiles.All(manifestMap.ContainsKey),
                    "Manifest covers exact bidirectional set of archive entries");
                foreach (string f in expectedFiles)
                {
                    string actualSha = Convert.ToHexString(SHA256.HashData(byName[f]));
                    if (!string.Equals(actualSha, manifestMap[f], StringComparison.OrdinalIgnoreCase))
                        Fail($"Manifest SHA-256 mismatch for {f}");
                }
                Pass("All manifest entry SHA-256 hashes verified");
            }

            // 4. On Windows: verify Authenticode signature + signer cert SPKI SHA-256 == authorKeyFingerprint
            if (OperatingSystem.IsWindows())
            {
                VerifyWindowsAuthenticode(extractedDll, authorFp);
            }
        }
        else
        {
            Pass("Unsigned archive accepted (no --require-signed flag)");
        }

        return extractedDll;
    }

    private static void VerifyWindowsAuthenticode(string dllPath, string expectedAuthorFp)
    {
        // Verify Authenticode cryptographic digest via WinVerifyTrust (allowing self-signed author cert,
        // matching DH 5's AuthenticodeVerifier behavior) and verify signer certificate SPKI matches entitlement.
#pragma warning disable SYSLIB0057 // CreateFromSignedFile is the built-in Authenticode PE extractor on Windows
        using var signedFileCert = X509Certificate.CreateFromSignedFile(dllPath);
#pragma warning restore SYSLIB0057
        using var cert = X509CertificateLoader.LoadCertificate(signedFileCert.GetRawCertData());
        string signerFp = Convert.ToHexString(SHA256.HashData(cert.PublicKey.ExportSubjectPublicKeyInfo()));
        Require(string.Equals(signerFp, expectedAuthorFp, StringComparison.OrdinalIgnoreCase),
            "Authenticode signer certificate SPKI SHA-256 matches entitlement authorKeyFingerprint");

        uint status = WinVerifyTrustFile(dllPath);
        // 0 = fully trusted; 0x800B0109 = CERT_E_UNTRUSTEDROOT (expected for self-signed community author certs)
        const uint certEUntrustedRoot = 0x800B0109;
        Require(status == 0 || status == certEUntrustedRoot,
            $"WinVerifyTrust digest valid for self-signed Authenticode (status=0x{status:X8})");
    }

    private static void VerifyPeTokensAndReferences(string dllPath, string sdkDllPath)
    {
        var alc = new IsolatedPluginLoadContext(sdkDllPath, isCollectible: true);
        try
        {
            var sdkAsm = alc.LoadFromAssemblyPath(sdkDllPath);
            var pluginAsm = alc.LoadFromAssemblyPath(dllPath);

            using var fs = File.OpenRead(dllPath);
            using var pe = new PEReader(fs);
            var mr = pe.GetMetadataReader();

            // 1. Check zero forbidden Windows desktop / WinForms / System.Media assembly refs
            var asmRefs = new List<string>();
            foreach (var handle in mr.AssemblyReferences)
            {
                var asmRef = mr.GetAssemblyReference(handle);
                string name = mr.GetString(asmRef.Name);
                asmRefs.Add(name);
                if (ForbiddenAssemblies.Contains(name, StringComparer.OrdinalIgnoreCase))
                    Fail("Forbidden desktop assembly reference found in DH 5 plugin: " + name);
            }
            Pass($"Zero System.Windows.Forms/System.Media references ({asmRefs.Count} clean assembly refs)");

            // 2. Resolve every TypeRef and MemberRef against loaded VoK.Sdk 5.3.0.0 + net10.0 runtime
            var unresolved = new List<string>();
            int typeRefCount = 0;
            foreach (var handle in mr.TypeReferences)
            {
                ++typeRefCount;
                var tr = mr.GetTypeReference(handle);
                string ns = mr.GetString(tr.Namespace);
                string name = mr.GetString(tr.Name);
                string fullName = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
                Type? resolved = ResolveTypeRef(mr, handle, alc, sdkAsm, pluginAsm);
                if (resolved is null)
                    unresolved.Add("TypeRef: " + fullName);
            }

            int memberRefCount = 0;
            foreach (var handle in mr.MemberReferences)
            {
                ++memberRefCount;
                var mem = mr.GetMemberReference(handle);
                string memberName = mr.GetString(mem.Name);
                Type? owner = ResolveEntityOwner(mr, mem.Parent, alc, sdkAsm, pluginAsm);
                if (owner is null)
                {
                    // Skip TypeSpec generic instantiations handled by runtime resolver if parent is TypeSpec
                    if (mem.Parent.Kind == HandleKind.TypeSpecification) continue;
                    unresolved.Add($"MemberRef owner unresolved: {memberName}");
                    continue;
                }
                if (!HasMatchingMember(owner, memberName))
                    unresolved.Add($"MemberRef missing: {owner.FullName}.{memberName}");
            }

            if (unresolved.Count > 0)
                Fail("Unresolved PE tokens:\n  " + string.Join("\n  ", unresolved.Take(20)));
            Pass($"All PE tokens resolved ({typeRefCount} TypeRefs, {memberRefCount} MemberRefs, 0 unresolved)");

            // 3. Verify DH 5 host security constraints (matches DungeonHelper.dll 퉧.퉍)
            VerifyDh5SecurityConstraints(mr);
        }
        finally
        {
            // Non-collectible isolated ALC required by DispatchProxy dynamic interface proxy emission.
        }
    }

    private static void VerifyDh5SecurityConstraints(MetadataReader mr)
    {
        var violations = new List<string>();

        foreach (var mh in mr.MethodDefinitions)
        {
            var md = mr.GetMethodDefinition(mh);
            if ((md.Attributes & MethodAttributes.PinvokeImpl) == 0) continue;
            var import = md.GetImport();
            string importName = mr.GetString(import.Name);
            string methodName = mr.GetString(md.Name);
            if (ForbiddenPInvokes.Contains(importName) || ForbiddenPInvokes.Contains(methodName))
                violations.Add($"Forbidden P/Invoke: {importName} ({methodName})");
        }

        foreach (var trh in mr.TypeReferences)
        {
            string fullName = GetTypeRefFullName(mr, mr.GetTypeReference(trh));
            if (ForbiddenSecurityTypes.Contains(fullName))
                violations.Add("Forbidden TypeRef: " + fullName);
        }

        foreach (var mrh in mr.MemberReferences)
        {
            var mem = mr.GetMemberReference(mrh);
            if (mem.Parent.Kind != HandleKind.TypeReference) continue;
            string ownerFullName = GetTypeRefFullName(mr, mr.GetTypeReference((TypeReferenceHandle)mem.Parent));
            string memberName = mr.GetString(mem.Name);
            if (ForbiddenSecurityMembers.Contains((ownerFullName, memberName)))
                violations.Add($"Forbidden MemberRef: {ownerFullName}.{memberName}");
        }

        foreach (var cah in mr.CustomAttributes)
        {
            var ca = mr.GetCustomAttribute(cah);
            string? attrTypeName = ResolveAttributeTypeName(mr, ca.Constructor);
            if (attrTypeName == "System.Security.UnverifiableCodeAttribute")
                violations.Add("Unsafe/unverifiable code attribute present: " + attrTypeName);
        }

        if (violations.Count > 0)
            Fail("DH 5 security constraint violation(s):\n  " + string.Join("\n  ", violations));
        Pass("DH 5 security verifier constraints passed (0 forbidden P/Invokes, types, members, or unsafe code)");
    }

    private static string GetTypeRefFullName(MetadataReader mr, TypeReference tr)
    {
        string ns = mr.GetString(tr.Namespace);
        string name = mr.GetString(tr.Name);
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }

    private static string? ResolveAttributeTypeName(MetadataReader mr, EntityHandle ctor)
    {
        if (ctor.Kind == HandleKind.MemberReference)
        {
            var mem = mr.GetMemberReference((MemberReferenceHandle)ctor);
            if (mem.Parent.Kind == HandleKind.TypeReference)
                return GetTypeRefFullName(mr, mr.GetTypeReference((TypeReferenceHandle)mem.Parent));
            if (mem.Parent.Kind == HandleKind.TypeDefinition)
            {
                var td = mr.GetTypeDefinition((TypeDefinitionHandle)mem.Parent);
                string ns = mr.GetString(td.Namespace);
                string name = mr.GetString(td.Name);
                return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
            }
        }
        else if (ctor.Kind == HandleKind.MethodDefinition)
        {
            var md = mr.GetMethodDefinition((MethodDefinitionHandle)ctor);
            var td = mr.GetTypeDefinition(md.GetDeclaringType());
            string ns = mr.GetString(td.Namespace);
            string name = mr.GetString(td.Name);
            return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        }
        return null;
    }

    private static Type? ResolveTypeRef(MetadataReader mr, TypeReferenceHandle handle, AssemblyLoadContext alc, Assembly sdkAsm, Assembly pluginAsm)
    {
        var tr = mr.GetTypeReference(handle);
        string ns = mr.GetString(tr.Namespace);
        string name = mr.GetString(tr.Name);
        if (tr.ResolutionScope.Kind == HandleKind.TypeReference)
        {
            var outer = ResolveTypeRef(mr, (TypeReferenceHandle)tr.ResolutionScope, alc, sdkAsm, pluginAsm);
            return outer?.GetNestedType(name, BindingFlags.Public | BindingFlags.NonPublic);
        }
        string fullName = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        if (tr.ResolutionScope.Kind == HandleKind.AssemblyReference)
        {
            string asmName = mr.GetString(mr.GetAssemblyReference((AssemblyReferenceHandle)tr.ResolutionScope).Name);
            if (asmName == "VoK.Sdk") return sdkAsm.GetType(fullName);
            try
            {
                var asm = alc.LoadFromAssemblyName(new AssemblyName(asmName));
                var t = asm.GetType(fullName);
                if (t is not null) return t;
            }
            catch { }
        }
        return sdkAsm.GetType(fullName) ?? pluginAsm.GetType(fullName) ?? Type.GetType(fullName);
    }

    private static Type? ResolveEntityOwner(MetadataReader mr, EntityHandle parent, AssemblyLoadContext alc, Assembly sdkAsm, Assembly pluginAsm)
    {
        return parent.Kind switch
        {
            HandleKind.TypeReference => ResolveTypeRef(mr, (TypeReferenceHandle)parent, alc, sdkAsm, pluginAsm),
            HandleKind.TypeDefinition => ResolveTypeDef(mr, (TypeDefinitionHandle)parent, pluginAsm),
            _ => null
        };
    }

    private static Type? ResolveTypeDef(MetadataReader mr, TypeDefinitionHandle handle, Assembly pluginAsm)
    {
        var td = mr.GetTypeDefinition(handle);
        string ns = mr.GetString(td.Namespace);
        string name = mr.GetString(td.Name);
        string fullName = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        return pluginAsm.GetType(fullName);
    }

    private static bool HasMatchingMember(Type owner, string memberName)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;
        if (memberName is ".ctor" or ".cctor")
            return owner.GetConstructors(flags).Length > 0 || owner.IsValueType;
        if (owner.GetMember(memberName, flags).Length > 0) return true;
        if (owner.IsInterface)
        {
            foreach (var iface in owner.GetInterfaces())
                if (iface.GetMember(memberName, flags).Length > 0) return true;
        }
        return false;
    }

    private static async Task VerifyIsolatedLoadAndBehaviorAsync(string dllPath, string sdkDllPath, string tempDir)
    {
        var alc = new IsolatedPluginLoadContext(sdkDllPath, isCollectible: false);
        try
        {
            var sdkAsm = alc.LoadFromAssemblyPath(sdkDllPath);
            var pluginAsm = alc.LoadFromAssemblyPath(dllPath);

            // 1. Assembly identity & attributes
            var companyAttr = pluginAsm.GetCustomAttribute<AssemblyCompanyAttribute>();
            Require(companyAttr?.Company == ExpectedCompany, $"AssemblyCompany is '{ExpectedCompany}'");

            var iPluginType = sdkAsm.GetType("VoK.Sdk.IPlugin")!;
            var pluginType = pluginAsm.GetTypes().Single(t => t.IsPublic && !t.IsAbstract && iPluginType.IsAssignableFrom(t));
            Pass($"Found VoK.Sdk.IPlugin implementation: {pluginType.FullName}");

            var idAttrType = sdkAsm.GetType("VoK.Sdk.PluginIdAttribute")!;
            var nameAttrType = sdkAsm.GetType("VoK.Sdk.PluginNameAttribute")!;
            var iconAttrType = sdkAsm.GetType("VoK.Sdk.PluginIconAttribute")!;

            var idAttr = pluginType.GetCustomAttribute(idAttrType);
            Require(idAttr is not null && string.Equals(idAttrType.GetProperty("Id")!.GetValue(idAttr)!.ToString(), ExpectedPluginId, StringComparison.OrdinalIgnoreCase),
                $"[PluginId] matches {ExpectedPluginId}");

            var nameAttr = pluginType.GetCustomAttribute(nameAttrType);
            Require(nameAttr is not null && (string?)nameAttrType.GetProperty("Name")!.GetValue(nameAttr) == ExpectedPluginName,
                $"[PluginName] matches '{ExpectedPluginName}'");

            var iconAttr = pluginType.GetCustomAttribute(iconAttrType);
            string? iconRes = iconAttr is not null ? (string?)iconAttrType.GetProperty("ResourceName")!.GetValue(iconAttr) : null;
            Require(iconRes == ExpectedIconResource, $"[PluginIcon] matches '{ExpectedIconResource}'");

            // 2. Verify embedded 36x36 PNG icon (matching DH 5's suffix lookup on [PluginIcon] + AssemblyExtensions.GetResourceBytes)
            string? fullIconResName = pluginAsm.GetManifestResourceNames()
                .FirstOrDefault(n => n.Equals(iconRes, StringComparison.OrdinalIgnoreCase) ||
                                     n.EndsWith("." + iconRes, StringComparison.OrdinalIgnoreCase));
            Require(fullIconResName is not null, $"Embedded manifest resource matching '{iconRes}' found ({fullIconResName})");
            var asmExtType = sdkAsm.GetType("VoK.Sdk.AssemblyExtensions")!;
            var iconBytes = (byte[]?)asmExtType.GetMethod("GetResourceBytes")!.Invoke(null, new object[] { pluginAsm, fullIconResName! });
            Require(iconBytes is { Length: > 24 }, "Embedded Icon.png loaded via VoK.Sdk.AssemblyExtensions.GetResourceBytes");
            VerifyPng36x36(iconBytes!);

            // 3. Instantiate plugin & check WindowVisible round-trip
            var plugin = Activator.CreateInstance(pluginType)!;
            var visProp = iPluginType.GetProperty("WindowVisible")!;
            Require((bool)visProp.GetValue(plugin)! == false, "WindowVisible defaults to false");
            visProp.SetValue(plugin, true);
            Require((bool)visProp.GetValue(plugin)! == true, "WindowVisible toggles to true");
            visProp.SetValue(plugin, false);
            Require((bool)visProp.GetValue(plugin)! == false, "WindowVisible toggles back to false");

            // 4. Mock Initialize + async scan + /alch commands + town suppression + ResetUi + Dispose
            var mockState = new MockDdoState();
            object mockData = DynamicMock.Create(sdkAsm.GetType("VoK.Sdk.Ddo.IDdoGameDataProvider")!, mockState, sdkAsm);
            object mockEvents = DynamicMock.Create(sdkAsm.GetType("VoK.Sdk.Ddo.IDdoEventProvider")!, mockState, sdkAsm);

            string pluginFolder = Path.Combine(tempDir, "plugin-data");
            iPluginType.GetMethod("Initialize")!.Invoke(plugin, new[] { mockData, mockEvents, pluginFolder });
            Pass("IPlugin.Initialize succeeded against mocked VoK.Sdk 5.3.0.0");

            Require(File.Exists(Path.Combine(pluginFolder, "reaction-timer.json")), "Settings file persisted on Initialize");
            Require(mockState.ChatHandler is not null, "/alch chat command registered via OnChatCommand");

            // Trigger deterministic async scan on Dh5TimerEngine (dispose timer first so no queued callback can race _tickRunning)
            var engineProp = pluginType.GetProperty("Engine", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var engine = engineProp.GetValue(plugin)!;
            var engineType = engine.GetType();
            var timer = (System.Threading.Timer)engineType.GetField("_timer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(engine)!;
            await timer.DisposeAsync();
            var tickRunningField = engineType.GetField("_tickRunning", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while ((int)tickRunningField.GetValue(engine)! != 0 && DateTime.UtcNow < deadline)
                await Task.Delay(10);
            var tickMethod = engineType.GetMethod("TickAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

            // Force next scan & await TickAsync
            engineType.GetField("_nextScan", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(engine, 0d);
            await (Task)tickMethod.Invoke(engine, null)!;

            var snap = engineType.GetMethod("Snapshot", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(engine, null)!;
            var view = snap.GetType().GetProperty("View")!.GetValue(snap)!;
            string mode = view.GetType().GetProperty("Mode")!.GetValue(view)!.ToString()!;
            string reaction = view.GetType().GetProperty("Reaction")!.GetValue(view)!.ToString()!;
            string baseStance = view.GetType().GetProperty("BaseStance")!.GetValue(view)!.ToString()!;
            double? remaining = (double?)view.GetType().GetProperty("Remaining")!.GetValue(view);
            int? spCur = (int?)snap.GetType().GetProperty("SpellPointsCurrent")!.GetValue(snap);
            int? spMax = (int?)snap.GetType().GetProperty("SpellPointsMax")!.GetValue(snap);

            string detail = view.GetType().GetProperty("Detail")!.GetValue(view)?.ToString() ?? "";
            string logPath = Path.Combine(pluginFolder, "reaction-timer.log");
            string logText = File.Exists(logPath) ? File.ReadAllText(logPath).Replace("\r", "").Replace("\n", " | ") : "(no log)";
            Require(mode == "Active" && reaction == "Pyrite" && remaining is > 1 and <= 12,
                $"Async scan detected active Pyrite spike ({remaining:0.0}s remaining via Expiration; got mode={mode}, reaction={reaction}, base={baseStance}, detail='{detail}', sp={spCur}/{spMax}, log='{logText}')");
            Require(baseStance == "Pyrite", "Async scan detected persistent Pyrite Reaction base stance");
            Require(spCur == 1200 && spMax == 2000, "Async scan captured live Spell Points (1200/2000)");

            // Test /alch chat commands
            await InvokeChatAsync(mockState, sdkAsm, "status");
            Require(mockState.PrintedLines.Any(l => l.Contains("PYRITE", StringComparison.OrdinalIgnoreCase)),
                "/alch status printed current Pyrite state");

            await InvokeChatAsync(mockState, sdkAsm, "warn 3.5");
            var settingsAfterWarn = engineType.GetMethod("GetSettings", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(engine, null)!;
            Require(Math.Abs((double)settingsAfterWarn.GetType().GetProperty("WarningSeconds")!.GetValue(settingsAfterWarn)! - 3.5) < 0.001,
                "/alch warn 3.5 persisted WarningSeconds");

            await InvokeChatAsync(mockState, sdkAsm, "hud donut");
            var settingsAfterHud = engineType.GetMethod("GetSettings", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(engine, null)!;
            Require(settingsAfterHud.GetType().GetProperty("HudMode")!.GetValue(settingsAfterHud)!.ToString() == "RadialDonut",
                "/alch hud donut switched HUD mode to RadialDonut");

            // Test all 3 DDO Material Dark themes via /alch theme
            foreach (var (cmdArg, expectedTheme) in new[] { ("cannith", "CannithArcane"), ("feywild", "FeywildTwilight"), ("eberron", "EberronGold") })
            {
                await InvokeChatAsync(mockState, sdkAsm, "theme " + cmdArg);
                var ts = engineType.GetMethod("GetSettings", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(engine, null)!;
                Require(ts.GetType().GetProperty("Theme")!.GetValue(ts)!.ToString() == expectedTheme,
                    $"/alch theme {cmdArg} persisted {expectedTheme}");
            }

            // Default Quick Re-Spike (bar=0, slot=0) must NOT invoke hotbar until configured
            await InvokeChatAsync(mockState, sdkAsm, "respike");
            Require(mockState.LastHotbarInvoke is null, "Unconfigured default Quick Re-Spike (0,0) safely declined InvokeHotbar");

            // Enable Quick Re-Spike and test /alch respike -> InvokeHotbar(1, 4, null)
            settingsAfterHud = engineType.GetMethod("GetSettings", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(engine, null)!;
            settingsAfterHud.GetType().GetProperty("QuickRespikeEnabled")!.SetValue(settingsAfterHud, true);
            settingsAfterHud.GetType().GetProperty("QuickRespikeBar")!.SetValue(settingsAfterHud, 2);
            settingsAfterHud.GetType().GetProperty("QuickRespikeSlot")!.SetValue(settingsAfterHud, 5);
            engineType.GetMethod("SaveSettings", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(engine, new[] { settingsAfterHud });
            await InvokeChatAsync(mockState, sdkAsm, "respike");
            Require(mockState.LastHotbarInvoke == (1u, 4u), "/alch respike invoked 0-based hotbar (bar 1, slot 4)");

            // Test InTownAsync() == true suppression
            mockState.InTown = true;
            mockState.ActiveSpikePresent = false;
            engineType.GetField("_nextScan", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(engine, 0d);
            await (Task)tickMethod.Invoke(engine, null)!;
            var townSnap = engineType.GetMethod("Snapshot", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(engine, null)!;
            var townView = townSnap.GetType().GetProperty("View")!.GetValue(townSnap)!;
            Require(townView.GetType().GetProperty("Mode")!.GetValue(townView)!.ToString() == "Paused" &&
                    (bool)townView.GetType().GetProperty("InTown")!.GetValue(townView)! &&
                    (long)townView.GetType().GetProperty("Alarm")!.GetValue(townView)! == 0L,
                "InTownAsync() == true suppressed expiry alarm and entered calm town-paused state");

            // Test ResetUi() and ClientFrameStep()
            iPluginType.GetMethod("ClientFrameStep")!.Invoke(plugin, null);
            bool resetOk = (bool)iPluginType.GetMethod("ResetUi")!.Invoke(plugin, null)!;
            Require(resetOk, "ResetUi() returned true");

            // Test Dispose()
            iPluginType.GetMethod("Dispose")!.Invoke(plugin, null);
            Require(mockState.ChatRemoved, "Dispose() cleanly unregistered /alch command and stopped worker");

            // 5. Verify zero-jitter tabular active countdown layout in Dh5ReactionTimerUi across 12.0 -> 0.1
            VerifyZeroJitterHudLayout(pluginAsm);
        }
        finally
        {
            // Non-collectible isolated ALC required by DispatchProxy dynamic interface proxy emission.
        }
    }

    private static void VerifyZeroJitterHudLayout(Assembly pluginAsm)
    {
        const BindingFlags staticFlags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        var uiType = pluginAsm.GetType("VoK.ReactionTimer.Dh5ReactionTimerUi")!;
        var viewType = pluginAsm.GetType("VoK.ReactionTimer.TimerView")!;
        var modeType = pluginAsm.GetType("VoK.ReactionTimer.TimerMode")!;
        var reactionType = pluginAsm.GetType("VoK.ReactionTimer.Reaction")!;
        var settingsType = pluginAsm.GetType("VoK.ReactionTimer.Settings")!;
        var presType = pluginAsm.GetType("VoK.ReactionTimer.HudPresentation")!;

        var measureTabular = uiType.GetMethod("MeasureTabularMetrics", staticFlags)!;
        var measurePill = uiType.GetMethod("MeasureCompactPillLayout", staticFlags)!;
        var measureDonut = uiType.GetMethod("MeasureRadialDonut", staticFlags)!;
        var computeRadialOrigin = uiType.GetMethod("ComputeRadialCountdownOrigin", staticFlags)!;
        var computeDotOffset = uiType.GetMethod("ComputeCountdownDotOffsetX", staticFlags)!;
        var countdownPrefix = uiType.GetMethod("CountdownPrefix", staticFlags)!;
        var textMethod = presType.GetMethod("Text", staticFlags)!;

        // Realistic proportional font metrics where narrow digits ('1'), wide curved digits ('8'),
        // and '.' have distinct widths AND flat vs curved digits differ in vertical height.
        static Vector2 ProportionalMeasure(float px, string text)
        {
            float scale = px / 28f;
            float w = 0f;
            float h = 28f * scale;
            foreach (char c in text)
            {
                w += c switch
                {
                    '1' => 10.0f * scale,
                    '4' or '7' => 14.5f * scale,
                    '0' or '2' or '3' or '5' or '6' or '8' or '9' => 18.0f * scale,
                    '.' => 5.5f * scale,
                    '~' => 16.5f * scale,
                    ' ' => 7.0f * scale,
                    _ => 15.0f * scale
                };
                if (c is '0' or '2' or '3' or '6' or '8' or '9')
                    h = MathF.Max(h, 29.4f * scale);
            }
            return new Vector2(w, h);
        }

        const int fontPx = 28;
        Func<string, Vector2> measureAtFont = s => ProportionalMeasure(fontPx, s);
        Func<float, string, Vector2> measureHint = (px, s) => ProportionalMeasure(px, s);
        object metrics = measureTabular.Invoke(null, new object[] { measureAtFont })!;

        int numPx = (int)MathF.Round(Math.Clamp(fontPx * 0.95f, 12f, 96f));
        Func<string, Vector2> measureAtNum = s => ProportionalMeasure(numPx, s);
        object numMetrics = measureTabular.Invoke(null, new object[] { measureAtNum })!;

        object settings = Activator.CreateInstance(settingsType)!;
        var activeMode = Enum.Parse(modeType, "Active");
        var pyrite = Enum.Parse(reactionType, "Pyrite");

        double[] ticks = { 12.0, 11.9, 11.1, 10.0, 9.9, 9.1, 8.8, 4.7, 2.0, 1.1, 0.1 };
        Vector2 vpSize = new(1920f, 1080f);
        float padX = Math.Max(12f, fontPx * 0.42f);
        float glyphW = Math.Max(12f, fontPx * 0.56f) + Math.Max(6f, fontPx * 0.22f);

        Vector2? firstPillSize = null;
        Vector2? firstPillMin = null;
        float? firstPillDotX = null;

        Vector2? firstDonutSize = null;
        Vector2? firstDonutOrigin = null;
        float? firstDonutDotX = null;

        foreach (double rem in ticks)
        {
            object view = Activator.CreateInstance(viewType, activeMode, pyrite, rem, 12d, false, 1L, "")!;
            string text = (string)textMethod.Invoke(null, new object[] { view, false })!;

            Vector2 pillSize = (Vector2)measurePill.Invoke(null, new object?[]
            {
                fontPx, text, view, false, settings, false, null, true, false, metrics, measureAtFont, measureHint
            })!;
            Vector2 pillMin = new(MathF.Round(0.5f * Math.Max(0f, vpSize.X - pillSize.X)), 0f);
            string prefix = (string)countdownPrefix.Invoke(null, new object[] { view, false })!;
            float pillDotX = pillMin.X + padX + glyphW + measureAtFont(prefix).X +
                             (float)computeDotOffset.Invoke(null, new object[] { rem, false, metrics })!;

            if (firstPillSize is null)
            {
                firstPillSize = pillSize;
                firstPillMin = pillMin;
                firstPillDotX = pillDotX;
            }
            else if (pillSize != firstPillSize.Value || pillMin != firstPillMin!.Value || MathF.Abs(pillDotX - firstPillDotX!.Value) > 0.001f)
            {
                Fail($"DH 5 CompactPill jitter at {rem:0.0}s: size {firstPillSize} -> {pillSize}, min {firstPillMin} -> {pillMin}, dotX {firstPillDotX} -> {pillDotX}");
            }

            Vector2 donutSize = (Vector2)measureDonut.Invoke(null, new object?[] { fontPx, null, true, false })!;
            Vector2 donutMin = new(MathF.Round(0.5f * Math.Max(0f, vpSize.X - donutSize.X)), 0f);
            float diameter = Math.Clamp(fontPx * 3.1f, 84f, 240f);
            Vector2 center = new(donutMin.X + donutSize.X * 0.5f, donutMin.Y + 10f + diameter * 0.5f);
            Vector2 numOrigin = (Vector2)computeRadialOrigin.Invoke(null, new object[] { center, rem, false, numMetrics })!;
            float donutDotX = numOrigin.X + (float)computeDotOffset.Invoke(null, new object[] { rem, false, numMetrics })!;

            if (firstDonutSize is null)
            {
                firstDonutSize = donutSize;
                firstDonutOrigin = numOrigin;
                firstDonutDotX = donutDotX;
            }
            else if (donutSize != firstDonutSize.Value || numOrigin != firstDonutOrigin!.Value || MathF.Abs(donutDotX - firstDonutDotX!.Value) > 0.001f)
            {
                Fail($"DH 5 RadialDonut jitter at {rem:0.0}s: size {firstDonutSize} -> {donutSize}, origin {firstDonutOrigin} -> {numOrigin}, dotX {firstDonutDotX} -> {donutDotX}");
            }
        }

        Vector2 baselinePill = firstPillSize!.Value;
        Vector2 baselineDonut = firstDonutSize!.Value;

        // Also check estimated '~' stability (12.0 -> 0.1) and >=100s 3-column stability (120.0 -> 100.1)
        Vector2 est12 = (Vector2)measurePill.Invoke(null, new object?[]
        {
            fontPx, "PYRITE  ~12.0", Activator.CreateInstance(viewType, activeMode, pyrite, 12.0d, 12d, true, 1L, "")!,
            false, settings, false, null, true, false, metrics, measureAtFont, measureHint
        })!;
        Vector2 est01 = (Vector2)measurePill.Invoke(null, new object?[]
        {
            fontPx, "PYRITE  ~0.1", Activator.CreateInstance(viewType, activeMode, pyrite, 0.1d, 12d, true, 1L, "")!,
            false, settings, false, null, true, false, metrics, measureAtFont, measureHint
        })!;
        Require(est12 == est01 && est12.X > baselinePill.X,
            $"DH 5 estimated '~' countdown preserves 0 px jitter across 12.0 -> 0.1 ({est12.X}x{est12.Y})");

        Vector2 long120 = (Vector2)measurePill.Invoke(null, new object?[]
        {
            fontPx, "PYRITE  120.0", Activator.CreateInstance(viewType, activeMode, pyrite, 120.0d, 120d, false, 1L, "")!,
            false, settings, false, null, true, false, metrics, measureAtFont, measureHint
        })!;
        Vector2 long100 = (Vector2)measurePill.Invoke(null, new object?[]
        {
            fontPx, "PYRITE  100.1", Activator.CreateInstance(viewType, activeMode, pyrite, 100.1d, 120d, false, 1L, "")!,
            false, settings, false, null, true, false, metrics, measureAtFont, measureHint
        })!;
        Require(long120 == long100 && long120.X > baselinePill.X,
            $"DH 5 >=100s 3-column countdown preserves 0 px jitter across 120.0 -> 100.1 ({long120.X}x{long120.Y})");

        Pass($"DH 5 CompactPill ({baselinePill.X}x{baselinePill.Y}) & RadialDonut ({baselineDonut.X}x{baselineDonut.Y}) exhibit 0 px jitter across 11 ticks (12.0 -> 0.1)");
    }

    private static async Task InvokeChatAsync(MockDdoState state, Assembly sdkAsm, string rest)
    {
        var cmdType = sdkAsm.GetType("VoK.Sdk.Ddo.IChatCommand")!;
        object cmd = DynamicMock.CreateChatCommand(cmdType, "alch", rest);
        var task = (Task)state.ChatHandler!.DynamicInvoke(cmd)!;
        await task;
    }

    private static void VerifyPng36x36(byte[] bytes)
    {
        byte[] sig = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        for (int i = 0; i < sig.Length; i++)
            if (bytes[i] != sig[i]) Fail("Icon.png does not start with valid PNG signature");
        int width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
        int height = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
        Require(width == 36 && height == 36, $"Icon.png is valid PNG with exact dimensions {width}x{height}");
    }

    // Minimal WinVerifyTrust interop (called only on Windows when verifying signed DH 5 archives)
    private static uint WinVerifyTrustFile(string filePath)
    {
        var fileInfo = new WINTRUST_FILE_INFO
        {
            cbStruct = (uint)Marshal.SizeOf<WINTRUST_FILE_INFO>(),
            pcwszFilePath = Marshal.StringToCoTaskMemUni(filePath)
        };
        IntPtr filePtr = Marshal.AllocCoTaskMem(Marshal.SizeOf<WINTRUST_FILE_INFO>());
        try
        {
            Marshal.StructureToPtr(fileInfo, filePtr, false);
            var data = new WINTRUST_DATA
            {
                cbStruct = (uint)Marshal.SizeOf<WINTRUST_DATA>(),
                dwUIChoice = 2, // WTD_UI_NONE
                fdwRevocationChecks = 0, // WTD_REVOKE_NONE
                dwUnionChoice = 1, // WTD_CHOICE_FILE
                pFile = filePtr,
                dwStateAction = 0, // WTD_STATEACTION_IGNORE
                dwProvFlags = 0x00000010 // WTD_REVOCATION_CHECK_NONE
            };
            Guid action = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
            return WinVerifyTrust(IntPtr.Zero, ref action, ref data);
        }
        finally
        {
            Marshal.FreeCoTaskMem(filePtr);
            Marshal.FreeCoTaskMem(fileInfo.pcwszFilePath);
        }
    }

    [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = false, CharSet = CharSet.Unicode)]
    private static extern uint WinVerifyTrust(IntPtr hwnd, ref Guid pgActionID, ref WINTRUST_DATA pWVTData);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WINTRUST_FILE_INFO
    {
        public uint cbStruct;
        public IntPtr pcwszFilePath;
        public IntPtr hFile;
        public IntPtr pgKnownSubject;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WINTRUST_DATA
    {
        public uint cbStruct;
        public IntPtr pPolicyCallbackData;
        public IntPtr pSIPClientData;
        public uint dwUIChoice;
        public uint fdwRevocationChecks;
        public uint dwUnionChoice;
        public IntPtr pFile;
        public uint dwStateAction;
        public IntPtr hWVTStateData;
        public IntPtr pwszURLReference;
        public uint dwProvFlags;
        public uint dwUIContext;
        public IntPtr pSignatureSettings;
    }

    private sealed class IsolatedPluginLoadContext : AssemblyLoadContext
    {
        private readonly string _sdkPath;
        public IsolatedPluginLoadContext(string sdkPath, bool isCollectible) : base(isCollectible: isCollectible) => _sdkPath = sdkPath;
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name == "VoK.Sdk") return LoadFromAssemblyPath(_sdkPath);
            return null;
        }
    }

    private sealed class MockDdoState
    {
        public bool InTown;
        public bool ActiveSpikePresent = true;
        public Delegate? ChatHandler;
        public bool ChatRemoved;
        public List<string> PrintedLines { get; } = new();
        public (uint Bar, uint Slot)? LastHotbarInvoke;
    }

    private class DynamicMock : DispatchProxy
    {
        private MockDdoState _state = null!;
        private Assembly _sdkAsm = null!;
        private Func<MethodInfo, object?[]?, object?>? _custom;

        public static object Create(Type interfaceType, MockDdoState state, Assembly sdkAsm)
        {
            var m = typeof(DispatchProxy).GetMethod(nameof(DispatchProxy.Create), Type.EmptyTypes)!.MakeGenericMethod(interfaceType, typeof(DynamicMock));
            var proxy = (DynamicMock)m.Invoke(null, null)!;
            proxy._state = state;
            proxy._sdkAsm = sdkAsm;
            return proxy;
        }

        public static object CreateCustom(Type interfaceType, Func<MethodInfo, object?[]?, object?> handler)
        {
            var m = typeof(DispatchProxy).GetMethod(nameof(DispatchProxy.Create), Type.EmptyTypes)!.MakeGenericMethod(interfaceType, typeof(DynamicMock));
            var proxy = (DynamicMock)m.Invoke(null, null)!;
            proxy._custom = handler;
            return proxy;
        }

        public static object CreateChatCommand(Type chatCmdType, string token, string rest)
        {
            return CreateCustom(chatCmdType, (mi, _) => mi.Name switch
            {
                "get_Token" => token,
                "get_Rest" => rest,
                _ => null
            });
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod is null) return null;
            if (_custom is not null) return _custom(targetMethod, args);

            string name = targetMethod.Name;
            Type ret = targetMethod.ReturnType;

            // IDdoEventProvider registrars
            if (ret.IsInterface && ret.Name.StartsWith("IEventRegistrar", StringComparison.Ordinal))
            {
                return CreateCustom(ret, (_, _) => null);
            }
            if (ret.IsInterface && ret.Name.StartsWith("IKeyedEventRegistrar", StringComparison.Ordinal))
            {
                return CreateCustom(ret, (mi, a) =>
                {
                    if (mi.Name == "AddHandler" && a is { Length: 2 } && (string?)a[0] == "alch")
                    {
                        _state.ChatHandler = a[1] as Delegate;
                        return true;
                    }
                    if (mi.Name == "RemoveHandler" && a is { Length: 1 } && (string?)a[0] == "alch")
                    {
                        _state.ChatRemoved = true;
                        return null;
                    }
                    return mi.ReturnType == typeof(bool) ? true : null;
                });
            }

            switch (name)
            {
                case "GetCurrentCharacterId":
                    return (ulong?)0x100001UL;
                case "InTownAsync":
                    return Task.FromResult<bool?>(_state.InTown);
                case "GetCurrentCharacterAsync":
                {
                    var entityType = _sdkAsm.GetType("VoK.Sdk.IEntity")!;
                    object entity = CreateCustom(entityType, (mi, _) => mi.Name switch
                    {
                        "get_SpellPoints_Current" => (int?)1200,
                        "get_SpellPoints_Max" => (int?)2000,
                        "get_Name" => "TestAlch",
                        _ => DefaultFor(mi.ReturnType)
                    });
                    return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(entityType).Invoke(null, new[] { entity });
                }
                case "GetActiveEffects":
                {
                    var effectType = _sdkAsm.GetType("VoK.Sdk.Ddo.IActiveEffect")!;
                    var dictType = typeof(Dictionary<,>).MakeGenericType(typeof(int), effectType);
                    var dict = Activator.CreateInstance(dictType)!;
                    var add = dictType.GetMethod("Add")!;

                    // Persistent Pyrite Reaction base stance (DID 2001, duration -1)
                    object baseStance = CreateCustom(effectType, (mi, _) => mi.Name switch
                    {
                        "get_EffectId" => 50,
                        "get_EffectDid" => 2001,
                        "get_Duration" => (float?)-1f,
                        "get_Expiration" => (DateTime?)DateTime.MaxValue,
                        "get_Created" => DateTime.UtcNow.AddMinutes(-5),
                        "get_IsPaused" => false,
                        _ => DefaultFor(mi.ReturnType)
                    });
                    add.Invoke(dict, new[] { (object)50, baseStance });

                    if (_state.ActiveSpikePresent)
                    {
                        object spike = CreateCustom(effectType, (mi, _) => mi.Name switch
                        {
                            "get_EffectId" => 51,
                            "get_EffectDid" => 2002,
                            "get_Duration" => (float?)12f,
                            "get_Expiration" => (DateTime?)DateTime.UtcNow.AddSeconds(8.5),
                            "get_Created" => DateTime.UtcNow.AddSeconds(-3.5),
                            "get_IsPaused" => false,
                            _ => DefaultFor(mi.ReturnType)
                        });
                        add.Invoke(dict, new[] { (object)51, spike });
                    }
                    return dict;
                }
                case "GetWeenieProperties":
                {
                    uint did = args is { Length: 1 } ? (uint)args[0]! : 0u;
                    string label = did == 2001u ? "Pyrite Reaction" : did == 2002u ? "Pyrite Reaction Spike" : "";
                    var propColType = _sdkAsm.GetType("VoK.Sdk.Properties.IPropertyCollection")!;
                    var sipType = _sdkAsm.GetType("VoK.Sdk.Properties.IStringInfoProperty")!;
                    var propType = _sdkAsm.GetType("VoK.Sdk.Properties.IProperty")!;
                    object sip = CreateCustom(sipType, (mi, _) => mi.Name switch
                    {
                        "get_PropertyName" => "Name",
                        "get_IsLiteral" => true,
                        "get_Text" => label,
                        "GetText" => label,
                        _ => DefaultFor(mi.ReturnType)
                    });
                    var listType = typeof(List<>).MakeGenericType(propType);
                    var list = Activator.CreateInstance(listType)!;
                    listType.GetMethod("Add")!.Invoke(list, new[] { sip });
                    return CreateCustom(propColType, (mi, _) => mi.Name switch
                    {
                        "get_Properties" => list,
                        _ => DefaultFor(mi.ReturnType)
                    });
                }
                case "Print":
                {
                    if (args is { Length: >= 1 } && args[0] is string line)
                        _state.PrintedLines.Add(line);
                    return true;
                }
                case "InvokeHotbar":
                {
                    if (args is { Length: >= 2 })
                        _state.LastHotbarInvoke = ((uint)args[0]!, (uint)args[1]!);
                    return true;
                }
                default:
                    return DefaultFor(ret);
            }
        }

        private static object? DefaultFor(Type t)
        {
            if (t == typeof(void)) return null;
            if (t == typeof(Task)) return Task.CompletedTask;
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var arg = t.GetGenericArguments()[0];
                return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(arg).Invoke(null, new[] { DefaultFor(arg) });
            }
            return t.IsValueType ? Activator.CreateInstance(t) : null;
        }
    }
}
