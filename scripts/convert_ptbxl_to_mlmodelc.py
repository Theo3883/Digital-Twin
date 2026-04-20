import os
import shutil
import subprocess
import sys
import types
from pathlib import Path
from typing import Any, Collection, Optional

import coremltools as ct
import torch


REPO_ROOT = Path(__file__).resolve().parents[1]
BENCHMARK_CODE_DIR = REPO_ROOT / "models" / "ecg_ptbxl_benchmarking" / "code"

if str(BENCHMARK_CODE_DIR) not in sys.path:
    sys.path.insert(0, str(BENCHMARK_CODE_DIR))


def install_fastai_v1_shims() -> None:
    # PTB-XL model code depends on fastai v1 symbols that are unavailable on Python 3.13.
    if "fastai.core" in sys.modules and "fastai.layers" in sys.modules:
        return

    core_module = types.ModuleType("fastai.core")
    layers_module = types.ModuleType("fastai.layers")

    def listify(value):
        if value is None:
            return []
        if isinstance(value, list):
            return value
        if isinstance(value, tuple):
            return list(value)
        return [value]

    class Flatten(torch.nn.Module):
        def forward(self, x):
            return x.view(x.size(0), -1)

    def bn_drop_lin(n_in: int, n_out: int, bn: bool = True, p: float = 0.0, actn=None):
        layers = [torch.nn.BatchNorm1d(n_in)] if bn else []
        if p != 0:
            layers.append(torch.nn.Dropout(p))
        layers.append(torch.nn.Linear(n_in, n_out))
        if actn is not None:
            layers.append(actn)
        return layers

    core_module.Optional = Optional
    core_module.Collection = Collection
    core_module.Floats = Any
    core_module.listify = listify
    layers_module.Flatten = Flatten
    layers_module.bn_drop_lin = bn_drop_lin

    if "fastai" not in sys.modules:
        sys.modules["fastai"] = types.ModuleType("fastai")
    sys.modules["fastai.core"] = core_module
    sys.modules["fastai.layers"] = layers_module


def get_xresnet1d101():
    install_fastai_v1_shims()
    from models.xresnet1d import xresnet1d101

    return xresnet1d101


DEFAULT_INPUT_CANDIDATES = [
    REPO_ROOT / "models" / "xresnet1d101_best.pt",
    REPO_ROOT
    / "models"
    / "ecg_ptbxl_benchmarking"
    / "output"
    / "exp0"
    / "models"
    / "fastai_xresnet1d101"
    / "models"
    / "fastai_xresnet1d101.pth",
]
TEMP_PACKAGE = REPO_ROOT / "models" / "PTBXLClassifier.mlpackage"
OUTPUT_MODELC = (
    REPO_ROOT
    / "SwiftUIApp"
    / "DigitalTwinApp"
    / "Resources"
    / "MLModels"
    / "PTBXLClassifier.mlmodelc"
)


def resolve_input_model() -> Path | None:
    override = os.environ.get("PTBXL_INPUT_MODEL")
    if override:
        candidate = Path(override).expanduser()
        if not candidate.is_absolute():
            candidate = REPO_ROOT / candidate
        return candidate if candidate.exists() else None

    for candidate in DEFAULT_INPUT_CANDIDATES:
        if candidate.exists():
            return candidate

    return None


def build_xresnet1d101(num_classes: int) -> torch.nn.Module:
    # Match the training defaults from fastai_model for fastai_xresnet1d101.
    xresnet1d101 = get_xresnet1d101()
    return xresnet1d101(
        num_classes=num_classes,
        input_channels=12,
        kernel_size=5,
        ps_head=0.5,
        lin_ftrs_head=[128],
    )


def load_checkpoint_model(input_model: Path) -> torch.nn.Module:
    print(f"Loading PyTorch checkpoint from {input_model}...")
    checkpoint = torch.load(
        str(input_model),
        map_location=torch.device("cpu"),
        weights_only=False,
    )

    if isinstance(checkpoint, torch.nn.Module):
        checkpoint.eval()
        return checkpoint

    state_dict = None
    if isinstance(checkpoint, dict):
        if "model" in checkpoint and isinstance(checkpoint["model"], dict):
            state_dict = checkpoint["model"]
        elif checkpoint and all(
            isinstance(k, str) and torch.is_tensor(v) for k, v in checkpoint.items()
        ):
            state_dict = checkpoint

    if state_dict is None:
        raise ValueError(
            "Unsupported checkpoint format. Expected a torch.nn.Module or state_dict payload."
        )

    if "8.8.weight" not in state_dict:
        raise KeyError("Could not infer class count from state_dict key '8.8.weight'.")

    num_classes = int(state_dict["8.8.weight"].shape[0])
    model = build_xresnet1d101(num_classes)
    model.load_state_dict(state_dict, strict=True)
    model.eval()
    return model


def compile_model_package(temp_package: Path, output_modelc: Path) -> bool:
    output_modelc.parent.mkdir(parents=True, exist_ok=True)
    if output_modelc.exists():
        shutil.rmtree(output_modelc)

    result = subprocess.run(
        ["xcrun", "coremlc", "compile", str(temp_package), str(output_modelc.parent)],
        capture_output=True,
        text=True,
        check=False,
    )
    if result.returncode != 0:
        if result.stdout:
            print(result.stdout)
        if result.stderr:
            print(result.stderr)
        return False
    return True


def convert_to_mlmodelc() -> None:
    input_model = resolve_input_model()
    if input_model is None:
        print("Error: Could not find a PTB-XL checkpoint file.")
        print("Checked default candidates:")
        for candidate in DEFAULT_INPUT_CANDIDATES:
            print(f"  - {candidate}")
        print("Tip: set PTBXL_INPUT_MODEL to an explicit path and rerun.")
        return

    model = load_checkpoint_model(input_model)

    # The model expects 1D conv input in [batch, channels, timesteps].
    print("Tracing the model with dummy tensor...")
    dummy = torch.randn(1, 12, 1000)
    traced = torch.jit.trace(model, dummy)

    print("Converting to CoreML representation...")
    mlmodel = ct.convert(
        traced,
        inputs=[ct.TensorType(shape=tuple(dummy.shape), name="ecg_signal")],
    )

    if TEMP_PACKAGE.exists():
        shutil.rmtree(TEMP_PACKAGE)

    print(f"Saving temporary package to {TEMP_PACKAGE}...")
    mlmodel.save(str(TEMP_PACKAGE))

    print("Compiling .mlpackage directly into .mlmodelc framework format...")
    success = compile_model_package(TEMP_PACKAGE, OUTPUT_MODELC)

    if success:
        print("\nSuccess! Compiled model exported to:")
        print(f"  {OUTPUT_MODELC}")
        if TEMP_PACKAGE.exists():
            shutil.rmtree(TEMP_PACKAGE)
        print("  (Cleaned up temporary .mlpackage cache)")
    else:
        print("\nCompilation failed. See output above for details.")


if __name__ == "__main__":
    convert_to_mlmodelc()
