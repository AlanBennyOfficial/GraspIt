[xml]$xml = Get-Content "TestResults\95121bf8-12b8-4bbd-b500-13948c25c846\coverage.cobertura.xml"
foreach ($pkg in $xml.coverage.packages.package) {
    Write-Host "Package: $($pkg.name), Line Rate: $($pkg.'line-rate')"
    if ($pkg.name -eq "GraspIt.Core" -or $pkg.name -eq "GraspIt.Application") {
        foreach ($class in $pkg.classes.class) {
            if ($class.'line-rate' -ne "1") {
                Write-Host "  Class $($class.name) line rate: $($class.'line-rate')"
            }
        }
    }
}
