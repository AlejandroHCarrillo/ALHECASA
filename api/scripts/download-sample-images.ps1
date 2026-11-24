<#
.SYNOPSIS
  Descarga fotos de ejemplo para las propiedades de demostración.

.DESCRIPTION
  Crea App_Data/sample-images/NN/ (NN = posición de la propiedad en SampleProperties.cs, 01 a 10) con entre 1 y 10
  fotos por propiedad. Las fotos son genéricas (picsum.photos, aleatorias pero estables por semilla): sirven para
  probar la galería, no representan cada inmueble. Con Seed:DemoAgency:SampleImagesPath configurado, la API las
  asigna a las propiedades de ejemplo que no tienen imágenes la próxima vez que arranca.
  La carpeta está en .gitignore; este script la recrea.
#>
param(
    [string]$Destination = (Join-Path $PSScriptRoot '..\src\Alhecasa.Api\App_Data\sample-images')
)

$ErrorActionPreference = 'Stop'

# Cantidad de fotos por propiedad, en el orden de SampleProperties.cs (todas entre 1 y 10).
$counts = 8, 6, 4, 5, 2, 3, 4, 10, 1, 3

for ($i = 0; $i -lt $counts.Count; $i++) {
    $folder = Join-Path $Destination ('{0:00}' -f ($i + 1))
    New-Item -ItemType Directory -Force $folder | Out-Null

    for ($k = 1; $k -le $counts[$i]; $k++) {
        $file = Join-Path $folder ('{0:00}.jpg' -f $k)
        if (Test-Path $file) { continue }
        $url = 'https://picsum.photos/seed/alhecasa-{0:00}-{1:00}/1200/800.jpg' -f ($i + 1), $k
        Invoke-WebRequest $url -OutFile $file -UseBasicParsing
    }

    Write-Host ('Propiedad {0:00}: {1} fotos' -f ($i + 1), $counts[$i])
}
