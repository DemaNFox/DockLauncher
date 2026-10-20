"""Generate the DocSoft window, executable and tray icons (requires Pillow)."""

from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1] / "src" / "AppHost" / "DockLauncher.AppHost" / "Resources"
SIZES = [(size, size) for size in (16, 24, 32, 48, 64, 128, 256)]


def draw_logo(size: int, tray: bool = False) -> Image.Image:
    scale = 4
    canvas = Image.new("RGBA", (size * scale, size * scale), (0, 0, 0, 0))
    draw = ImageDraw.Draw(canvas)

    def box(bounds: tuple[int, int, int, int], radius: int, color: str, outline: str | None = None, width: int = 1) -> None:
        factor = size * scale / 256
        draw.rounded_rectangle(
            tuple(round(value * factor) for value in bounds),
            radius=round(radius * factor),
            fill=color,
            outline=outline,
            width=max(1, round(width * factor)),
        )

    box((8, 8, 248, 248), 54, "#101C28", "#35555A" if tray else "#29454B", 5)
    box((44, 161, 212, 194), 16, "#63D5BF")
    box((51, 92, 94, 139), 12, "#368F88")
    box((106, 59, 150, 139), 12, "#A1EAD8")
    box((162, 92, 205, 139), 12, "#368F88")
    return canvas.resize((size, size), Image.Resampling.LANCZOS)


def main() -> None:
    ROOT.mkdir(parents=True, exist_ok=True)
    draw_logo(256).save(ROOT / "DocSoft.png")
    draw_logo(256).save(ROOT / "DocSoft.ico", format="ICO", sizes=SIZES)
    draw_logo(256, tray=True).save(ROOT / "DocSoft.Tray.ico", format="ICO", sizes=SIZES)


if __name__ == "__main__":
    main()
