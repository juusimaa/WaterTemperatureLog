// Regenerates the raster site icons from the same geometry as wwwroot/favicon.svg.
//
//   swift Resources/GenerateIcons.swift wwwroot
//
// Writes wwwroot/favicon.png (64px) and wwwroot/apple-touch-icon.png (180px).
// macOS only — it draws with CoreGraphics rather than rasterising the SVG, since
// no SVG converter (rsvg-convert, ImageMagick) is assumed to be installed.
//
// This file is not part of the dotnet build; the .csproj globs only *.cs.
//
// IMPORTANT: the constants below duplicate the SVG. If you change the icon,
// edit wwwroot/favicon.svg and this file together, then re-run the command above.

import Foundation
import CoreGraphics
import ImageIO
import UniformTypeIdentifiers

// The design space, matching the SVG's viewBox.
let D: CGFloat = 64

let inkColor = 0x234A34   // --green-deep, the backdrop
let lineColor = 0xE9ECE4  // --paper, the two saw lines

func c(_ hex: Int) -> CGColor {
    CGColor(red: CGFloat((hex >> 16) & 0xff)/255,
            green: CGFloat((hex >> 8) & 0xff)/255,
            blue: CGFloat(hex & 0xff)/255, alpha: 1)
}

/// One saw line centred on `y`, drawn as four sharp segments.
/// CoreGraphics has a bottom-left origin, so y is flipped to match the SVG.
func zigzag(_ ctx: CGContext, y: CGFloat) {
    let x0: CGFloat = 13, x1: CGFloat = 51, amp: CGFloat = 7, n = 4
    let step = (x1 - x0) / CGFloat(n)
    ctx.move(to: CGPoint(x: x0, y: D - (y + amp/2)))
    for i in 1...n {
        ctx.addLine(to: CGPoint(x: x0 + step * CGFloat(i),
                                y: D - (y + ((i % 2 == 1) ? -amp/2 : amp/2))))
    }
    ctx.strokePath()
}

func render(_ px: Int, opaqueBackdrop: Bool) -> CGImage {
    let ctx = CGContext(data: nil, width: px, height: px, bitsPerComponent: 8,
                        bytesPerRow: 0, space: CGColorSpaceCreateDeviceRGB(),
                        bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
    ctx.scaleBy(x: CGFloat(px)/D, y: CGFloat(px)/D)
    ctx.setLineCap(.round)
    ctx.setLineJoin(.miter)
    ctx.setMiterLimit(4)

    ctx.setFillColor(c(inkColor))
    if opaqueBackdrop {
        // iOS applies its own corner rounding to apple-touch-icon, so fill
        // edge to edge instead of rounding the corners here.
        ctx.fill(CGRect(x: 0, y: 0, width: D, height: D))
    } else {
        ctx.addPath(CGPath(roundedRect: CGRect(x: 2, y: 2, width: 60, height: 60),
                           cornerWidth: 14, cornerHeight: 14, transform: nil))
        ctx.fillPath()
    }

    ctx.setStrokeColor(c(lineColor))
    ctx.setLineWidth(6)
    zigzag(ctx, y: 25)
    zigzag(ctx, y: 40)
    return ctx.makeImage()!
}

func write(_ img: CGImage, _ path: String) {
    let dest = CGImageDestinationCreateWithURL(URL(fileURLWithPath: path) as CFURL,
                                              UTType.png.identifier as CFString, 1, nil)!
    CGImageDestinationAddImage(dest, img, nil)
    CGImageDestinationFinalize(dest)
}

guard CommandLine.arguments.count > 1 else {
    FileHandle.standardError.write("usage: swift GenerateIcons.swift <wwwroot-path>\n".data(using: .utf8)!)
    exit(1)
}
let root = CommandLine.arguments[1]
write(render(64, opaqueBackdrop: false), "\(root)/favicon.png")
write(render(180, opaqueBackdrop: true), "\(root)/apple-touch-icon.png")
print("wrote \(root)/favicon.png (64) and \(root)/apple-touch-icon.png (180)")
