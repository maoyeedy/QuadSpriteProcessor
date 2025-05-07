# Quad-Divisible Sprite Processor

[![openupm](https://img.shields.io/npm/v/com.maoyeedy.quad-sprite-processor?label=openupm&registry_uri=https://package.openupm.com)](https://openupm.com/packages/com.maoyeedy.quad-sprite-processor/)

<img src="Documentation~/editor-warning.png" width="600" alt="Editor Warning"/>

## Why Quad-Divisible?

During school projects, artists often gave me sprites with dimensions like 543x981 or 1381x737. 

Those can't be compressed to DXTn/BCn formats, thus bloating our final build size.

However, By resizing their dimensions to multiples of four, we witness significant smaller size:

<div align="center">
  <table>
    <tr>
      <td align="center"><img src="Documentation~/size-original.png" width="400" alt="Original texture"/><br><b>Original</b></td>
      <td align="center"><img src="Documentation~/size-quad-divisible.png" width="400" alt="Quad-divisible texture"/><br><b>Quad-Divisible (Way Smaller)</b></td>
    </tr>
  </table>
</div>

## Features

- Resize textures to be quad-divisible with minimal quality loss
- Batch process multiple textures at once
- Allow considering imported size, instead of original size

## How to Install

Package Manager - *Install Package from Git URL*
```
https://github.com/Maoyeedy/QuadSpriteProcessor.git
```

Or use [OpenUPM CLI](https://openupm.com/packages/com.maoyeedy.quad-sprite-processor/)
```
openupm add com.maoyeedy.quad-sprite-processor
```

Note: Only supports Unity **2021.3** Onwards. (As it utilizes `Texture2D.Reinitialize`)

## How to Use

### 1. Context Menu
- (Multi-Select and) right-click assets in project panel.
- *Resize to be Quad-Divisible*

### 2. Editor window
- *Tools - Texture Processing*
- Set path, Scan, Select, Process

 <img src="Documentation~/editor-window.png" width="400" alt="Editor Warning"/>

# Miscs

## TODO
- Bug Fixes (There can definitely be potential bugs, so Issues/PR are welcome)
- An option to use it as AssetPostprocessor, with some matching rules. So that it will auto-convert every sprite you import.
- Backup of the original sprites? (I think this bloats your unity project size. Also if you want to revert them, why not rely on VCS)
- For Tansparent-Background PNG, instead of resize, add transparent pixel to the border.
- Option to "Round to nearest power of two" (For mipmap needs)

## Why I made this

As most of my projects are showcased with WebGL, I've been trying to squeeze my build size even smaller for faster loading time. 

I once optimized a 2D game WebGL Build from 100MB to merely 40MB. For that, I wrote a [custom Powershell script](https://gist.github.com/Maoyeedy/769ad8f2f4faf3f5c219b07658bc3880) to recursively process all textures. However, that requires CLI and ImageMagick, so I made it integrated into Unity Editor to be more user-friendly.

Lastly, if you have never think about build size, I highly recommend [ProjectAuditor](https://github.com/Unity-Technologies/ProjectAuditor), or manually open your Editor.log to check 'Build Report'. You may be very likely surprised at the results.
