import SwiftUI

struct EcgWaveformPath: Shape {
    let samples: [Double]
    let yRangeMv: ClosedRange<Double>

    func path(in rect: CGRect) -> Path {
        guard samples.count > 1 else { return Path() }
        var path = Path()
        let stepX = rect.width / CGFloat(samples.count - 1)
        let minV = yRangeMv.lowerBound
        let maxV = yRangeMv.upperBound
        let range = maxV - minV

        for (i, sample) in samples.enumerated() {
            let x = CGFloat(i) * stepX
            // Map mV to screen y with a fixed calibrated range.
            let clamped = min(max(sample, minV), maxV)
            let t = (maxV - clamped) / range // 0 at top, 1 at bottom
            let y = rect.minY + CGFloat(t) * rect.height
            if i == 0 { path.move(to: CGPoint(x: x, y: y)) }
            else { path.addLine(to: CGPoint(x: x, y: y)) }
        }
        return path
    }
}

