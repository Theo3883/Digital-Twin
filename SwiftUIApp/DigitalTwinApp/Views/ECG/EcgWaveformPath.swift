import SwiftUI

struct EcgWaveformPath: Shape {
    let samples: [Double]

    func path(in rect: CGRect) -> Path {
        guard samples.count > 1 else { return Path() }
        var path = Path()
        let stepX = rect.width / CGFloat(samples.count - 1)
        let midY = rect.midY
        let scaleY = rect.height / 3.0

        for (i, sample) in samples.enumerated() {
            let x = CGFloat(i) * stepX
            let y = midY - CGFloat(sample) * scaleY
            if i == 0 { path.move(to: CGPoint(x: x, y: y)) }
            else { path.addLine(to: CGPoint(x: x, y: y)) }
        }
        return path
    }
}

