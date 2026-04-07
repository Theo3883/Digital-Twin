import Foundation
import CreateML

// Trains a Create ML text classifier from a folder structure:
//   models/training_data/<Label>/*.txt
// and writes:
//   models/output/doc_type_classifier_v1.mlmodel
//
// Usage:
//   swift scripts/train_text_classifier.swift models/training_data models/output/doc_type_classifier_v1.mlmodel

func die(_ message: String) -> Never {
    fputs("ERROR: \(message)\n", stderr)
    exit(1)
}

let args = CommandLine.arguments
guard args.count >= 3 else {
    die("Missing args. Usage: swift scripts/train_text_classifier.swift <training_dir> <output_mlmodel_path>")
}

let trainingDir = URL(fileURLWithPath: args[1], isDirectory: true)
let outputPath = URL(fileURLWithPath: args[2], isDirectory: false)

let fm = FileManager.default
guard fm.fileExists(atPath: trainingDir.path) else {
    die("Training dir not found: \(trainingDir.path)")
}

// Collect examples
var texts: [String] = []
var labels: [String] = []

let labelDirs: [URL]
do {
    labelDirs = try fm.contentsOfDirectory(at: trainingDir, includingPropertiesForKeys: [.isDirectoryKey], options: [.skipsHiddenFiles])
        .filter { url in
            (try? url.resourceValues(forKeys: [.isDirectoryKey]).isDirectory) == true
        }
} catch {
    die("Failed to list training directory: \(error)")
}

for labelDir in labelDirs.sorted(by: { $0.lastPathComponent < $1.lastPathComponent }) {
    let label = labelDir.lastPathComponent
    let files: [URL]
    do {
        files = try fm.contentsOfDirectory(at: labelDir, includingPropertiesForKeys: nil, options: [.skipsHiddenFiles])
            .filter { $0.pathExtension.lowercased() == "txt" }
    } catch {
        die("Failed to list label dir \(labelDir.path): \(error)")
    }

    for file in files {
        do {
            let text = try String(contentsOf: file, encoding: .utf8)
                .trimmingCharacters(in: .whitespacesAndNewlines)
            if text.isEmpty { continue }
            texts.append(text)
            labels.append(label)
        } catch {
            die("Failed to read \(file.path): \(error)")
        }
    }
}

guard texts.count == labels.count, texts.count > 0 else {
    die("No training examples found under: \(trainingDir.path)")
}

// Build MLDataTable
var dict: [String: MLDataValueConvertible] = [:]
dict["text"] = texts
dict["label"] = labels

let data = try MLDataTable(dictionary: dict)

// Train with default parameters (CreateML chooses the best available algorithm).
// Depending on macOS/Xcode version, the transfer-learning enum case may require arguments.
let params = MLTextClassifier.ModelParameters()
let classifier = try MLTextClassifier(trainingData: data, textColumn: "text", labelColumn: "label", parameters: params)

// Ensure output folder exists
try fm.createDirectory(at: outputPath.deletingLastPathComponent(), withIntermediateDirectories: true)

try classifier.write(to: outputPath)
print("OK: wrote \(outputPath.path)")

