import SwiftUI

struct ContentView_Previews: PreviewProvider {
    static var previews: some View {
        let container = AppContainer()
        ContentView()
            .environmentObject(container)
            .environmentObject(container.engine)
    }
}

