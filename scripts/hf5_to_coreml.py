import coremltools as ct
import tensorflow as tf
import os
import shutil

# Resolve paths relative to the script location
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
ROOT_DIR = os.path.dirname(SCRIPT_DIR)

# 1. Load the pretrained Ribeiro ECG model
model_path = os.path.join(ROOT_DIR, 'models', 'Ribeiro et al. — Nature Communications ResNet', 'model.hdf5')
print(f"Loading model from: {model_path}")
model = tf.keras.models.load_model(model_path)
print(f"Model type: {type(model)}")

# 2. Convert to CoreML via SavedModel with explicit signature
print("Converting model to CoreML...")

temp_saved_model_path = os.path.join(ROOT_DIR, 'models', 'temp_saved_model')
if os.path.exists(temp_saved_model_path):
    shutil.rmtree(temp_saved_model_path)

# Ensure the model has a single signature for coremltools
print("Step A: Saving model with explicit signature...")

@tf.function(input_signature=[tf.TensorSpec(shape=(1, 4096, 12), dtype=tf.float32, name="ecg_signal")])
def serving_fn(ecg_signal):
    return model(ecg_signal, training=False)

tf.saved_model.save(
    model, 
    temp_saved_model_path, 
    signatures={'serving_default': serving_fn}
)

# Step B: Convert SavedModel to CoreML
print("Step B: Converting SavedModel to CoreML (mlprogram)...")
mlmodel = ct.convert(
    temp_saved_model_path,
    source='tensorflow',
    convert_to="mlprogram"
)

# Cleanup
if os.path.exists(temp_saved_model_path):
    shutil.rmtree(temp_saved_model_path)








# 3. Save as .mlpackage directly to the Swift folder
output_dir = os.path.join(ROOT_DIR, 'SwiftUIApp', 'DigitalTwinApp', 'Resources', 'MLModels')
output_path = os.path.join(output_dir, "ECGClassifier.mlpackage")

# Ensure destination directory exists
os.makedirs(output_dir, exist_ok=True)

# Remove existing package if it exists (ct.save doesn't always overwrite directories cleanly)
if os.path.exists(output_path):
    print(f"Removing existing model at {output_path}")
    shutil.rmtree(output_path)

print(f"Saving CoreML model to: {output_path}")
mlmodel.save(output_path)
print("Conversion complete.")