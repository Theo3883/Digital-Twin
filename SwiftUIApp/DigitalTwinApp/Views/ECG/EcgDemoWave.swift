import SwiftUI

struct EcgDemoWave: Shape {
    var phase: Double

    var animatableData: Double {
        get { phase }
        set { phase = newValue }
    }

    func path(in rect: CGRect) -> Path {
        var path = Path()
        let steps = 200
        let stepX = rect.width / CGFloat(steps)
        let midY = rect.midY
        let scaleY = rect.height / 3.0

        for i in 0...steps {
            let t = Double(i) / Double(steps) + phase
            let pWave = 0.15 * sin(2 * .pi * t * 5)
            let qrs = sin(2 * .pi * t * 1.2)
            let qrsComponent = (abs(qrs) > 0.95) ? 1.0 * qrs : 0
            let tWave = 0.25 * sin(2 * .pi * (t - 0.3) * 3)
            let y = midY - CGFloat(pWave + qrsComponent + tWave) * scaleY
            let x = CGFloat(i) * stepX

            if i == 0 { path.move(to: CGPoint(x: x, y: y)) }
            else { path.addLine(to: CGPoint(x: x, y: y)) }
        }
        return path
    }
}

