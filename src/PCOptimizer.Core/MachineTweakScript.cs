using System.Globalization;

namespace PCOptimizer.Core;

// Script inputs are closed catalog identities, booleans and UInt32 values, never user code or paths.
public static class MachineTweakScript
{
    public static string Build(IReadOnlyList<TweakValueChange> changes)
    {
        if (changes.Count is < 1 or > 64) throw new InvalidOperationException("Tamaño de lote elevado no admitido.");
        var requests = new List<string>();
        var hasPolicies = false;
        foreach (var change in changes)
        {
            var target = ExecutableTweaks.Target(change.TweakId, change.TargetId);
            if (!target.Machine || (change.Before.Exists && change.Before.Kind != "DWord") || (change.After.Exists && change.After.Kind != "DWord"))
                throw new InvalidOperationException("El lote elevado solo admite los destinos DWORD del equipo de la lista cerrada.");
            if (target.Path.StartsWith(@"SOFTWARE\Policies\", StringComparison.OrdinalIgnoreCase))
            {
                hasPolicies = true;
            }
            static string Literal(string text) => "'" + text.Replace("'", "''", StringComparison.Ordinal) + "'";
            static string Number(RegistryValueSnapshot snapshot) => snapshot.Exists
                ? uint.Parse(snapshot.Data, NumberStyles.None, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture) : "0";
            requests.Add($"[pscustomobject]@{{ Path={Literal(target.Path)}; Name={Literal(target.Name)}; BeforeExists=${change.Before.Exists.ToString().ToLowerInvariant()}; Before=[uint32]{Number(change.Before)}; AfterExists=${change.After.Exists.ToString().ToLowerInvariant()}; After=[uint32]{Number(change.After)} }}");
        }
        // Only compiled registry paths and validated uint32 values enter this fixed script.
        // No elevated job files, arbitrary imports, user script text, or output file writes.
        var script = $$"""
            $ErrorActionPreference = 'Stop'
            $env:PSModulePath = [IO.Path]::Combine($PSHOME, 'Modules')
            function Test-Original($root, $request) {
                $key = $root.OpenSubKey($request.Path, $false)
                try {
                    $exists = $null -ne $key -and $key.GetValueNames() -contains $request.Name
                    if ($exists -ne $request.BeforeExists) { return $false }
                    if (-not $exists) { return $true }
                    if ($key.GetValueKind($request.Name) -ne [Microsoft.Win32.RegistryValueKind]::DWord) { return $false }
                    $signed = [BitConverter]::ToInt32([BitConverter]::GetBytes([uint32]$request.Before), 0)
                    return $key.GetValue($request.Name) -eq $signed
                } finally { if ($null -ne $key) { $key.Dispose() } }
            }
            try {
                {{(hasPolicies ? "if ((Get-CimInstance Win32_ComputerSystem -ErrorAction Stop).PartOfDomain) { exit 5 }" : "")}}
                $requests = @({{string.Join(",\n", requests)}})
                $root = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine, [Microsoft.Win32.RegistryView]::Registry64)
                try {
                    foreach ($request in $requests) { if (-not (Test-Original $root $request)) { exit 4 } }
                    foreach ($request in $requests) {
                        if (-not (Test-Original $root $request)) { exit 4 }
                        if ($request.AfterExists) { $key = $root.CreateSubKey($request.Path, $true) }
                        else { $key = $root.OpenSubKey($request.Path, $true) }
                        try {
                            if ($request.AfterExists) {
                                $signed = [BitConverter]::ToInt32([BitConverter]::GetBytes([uint32]$request.After), 0)
                                $key.SetValue($request.Name, $signed, [Microsoft.Win32.RegistryValueKind]::DWord)
                            } elseif ($null -ne $key) { $key.DeleteValue($request.Name, $false) }
                            if ($null -ne $key) { $key.Flush() }
                        } finally { if ($null -ne $key) { $key.Dispose() } }
                    }
                } finally { $root.Dispose() }
                exit 0
            } catch { exit 2 }
            """;
        return script;
    }
}
