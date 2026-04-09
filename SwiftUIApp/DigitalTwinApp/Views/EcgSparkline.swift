import SwiftUI

struct EcgSparkline: View {
    var body: some View {
        GeometryReader { geo in
            Path { path in
                let w = geo.size.width
                let h = geo.size.height
                // Same shape as the MAUI SVG: M0,20 L10,20 L15,5 L20,25 L25,20 L40,20 L45,10 L50,28 L55,20 L100,20
                let points: [(CGFloat, CGFloat)] = [
                    (0, 0.67), (0.10, 0.67), (0.15, 0.17), (0.20, 0.83),
                    (0.25, 0.67), (0.40, 0.67), (0.45, 0.33), (0.50, 0.93),
                    (0.55, 0.67), (1.0, 0.67)
                ]
                path.move(to: CGPoint(x: points[0].0 * w, y: points[0].1 * h))
                for pt in points.dropFirst() {
                    path.addLine(to: CGPoint(x: pt.0 * w, y: pt.1 * h))
                }
            }
            .stroke(LiquidGlass.redCritical, lineWidth: 2)
        }
    }
}

