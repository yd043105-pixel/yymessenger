param()
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationCore,WindowsBase,PresentationFramework
$folder=Join-Path (Split-Path $PSScriptRoot -Parent) 'src\SchoolMessenger.Shared\Branding'
[xml]$svg=Get-Content -LiteralPath (Join-Path $folder 'brand_mark.svg') -Raw -Encoding UTF8
function Brush([string]$color){[Windows.Media.SolidColorBrush]::new([Windows.Media.ColorConverter]::ConvertFromString($color))}
function Render([int]$size){
    $visual=[Windows.Media.DrawingVisual]::new();$context=$visual.RenderOpen()
    try{
        $context.PushTransform([Windows.Media.ScaleTransform]::new($size/128.0,$size/128.0))
        foreach($element in $svg.DocumentElement.ChildNodes){
            if($element.LocalName -eq 'rect'){$context.DrawRoundedRectangle((Brush $element.fill),$null,[Windows.Rect]::new(0,0,[double]$element.width,[double]$element.height),[double]$element.rx,[double]$element.rx)}
            elseif($element.LocalName -eq 'path'){$context.DrawGeometry((Brush $element.fill),$null,[Windows.Media.Geometry]::Parse($element.d))}
        }
        $context.Pop()
    }finally{$context.Close()}
    $bitmap=[Windows.Media.Imaging.RenderTargetBitmap]::new($size,$size,96,96,[Windows.Media.PixelFormats]::Pbgra32);$bitmap.Render($visual)
    $encoder=[Windows.Media.Imaging.PngBitmapEncoder]::new();$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream=[IO.MemoryStream]::new();try{$encoder.Save($stream);return ,$stream.ToArray()}finally{$stream.Dispose()}
}
[IO.File]::WriteAllBytes((Join-Path $folder 'brand_mark.png'),(Render 512))
$sizes=@(16,20,24,32,48,64,128,256);$frames=@($sizes|ForEach-Object {,$(Render $_)})
$output=[IO.File]::Create((Join-Path $folder 'brand_mark.ico'));$writer=[IO.BinaryWriter]::new($output)
try{
    $writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$sizes.Count);$offset=6+16*$sizes.Count
    for($i=0;$i -lt $sizes.Count;$i++){
        $dimension=if($sizes[$i] -eq 256){0}else{$sizes[$i]}
        $writer.Write([byte]$dimension);$writer.Write([byte]$dimension);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$frames[$i].Length);$writer.Write([uint32]$offset);$offset+=$frames[$i].Length
    }
    foreach($frame in $frames){$writer.Write([byte[]]$frame)}
}finally{$writer.Dispose()}
Write-Host 'Generated 512px preview and Windows icon with 16/20/24/32/48/64/128/256px frames from the shared SVG.'
