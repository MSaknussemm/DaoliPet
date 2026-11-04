$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$wpf = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF'
$compilerArgs = @('/nologo', '/target:winexe', '/platform:x64', '/optimize+',
    ('/out:' + (Join-Path $root 'DaoliPet.exe')),
    ('/win32icon:' + (Join-Path $root 'daoli.ico')),
    ('/reference:' + $wpf + '\PresentationCore.dll'),
    ('/reference:' + $wpf + '\PresentationFramework.dll'),
    ('/reference:' + $wpf + '\WindowsBase.dll'),
    '/reference:System.Xaml.dll', '/reference:System.Xml.Linq.dll',
    '/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll',
    (Join-Path $PSScriptRoot 'DaoliPet.cs'), (Join-Path $PSScriptRoot 'ThrowPhysics.cs'), (Join-Path $PSScriptRoot 'PhysicalPose.cs'))
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' $compilerArgs
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
