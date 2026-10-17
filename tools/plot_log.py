"""Plot one Unity tank run: level vs. float thresholds, pump state, and fault periods.

Usage:  python tools/plot_log.py unity/TankLogs/run_YYYYMMDD_HHMMSS.csv [--low 0.2 --high 0.9]
Writes a PNG next to the CSV.
"""
import argparse
from pathlib import Path

import matplotlib.pyplot as plt
import pandas as pd


def shade(ax, t, mask, color, label):
    """Shade every contiguous span where mask is true."""
    start = None
    first = True
    for ti, m in zip(t, mask):
        if m and start is None:
            start = ti
        elif not m and start is not None:
            ax.axvspan(start, ti, color=color, alpha=0.2, lw=0, label=label if first else None)
            first, start = False, None
    if start is not None:
        ax.axvspan(start, t.iloc[-1], color=color, alpha=0.2, lw=0, label=label if first else None)


def main():
    p = argparse.ArgumentParser()
    p.add_argument("csv")
    p.add_argument("--low", type=float, default=0.2)
    p.add_argument("--high", type=float, default=0.9)
    args = p.parse_args()

    df = pd.read_csv(args.csv)
    t = df["t"] - df["t"].iloc[0]

    fig, (ax1, ax2) = plt.subplots(2, 1, sharex=True, figsize=(10, 5),
                                   gridspec_kw={"height_ratios": [3, 1]})

    ax1.plot(t, df["level"] * 100, color="#1f6feb", lw=2, label="Tank level")
    ax1.axhline(args.high * 100, color="gray", ls="--", lw=1, label="High float")
    ax1.axhline(args.low * 100, color="gray", ls=":", lw=1, label="Low float")
    shade(ax1, t, df["comms_fault"] == 1, "orange", "Comms fault")
    shade(ax1, t, df["sensor_fault"] == 1, "red", "Sensor fault")
    shade(ax1, t, df["estop_ok"] == 0, "purple", "E-stop")
    ax1.set_ylabel("Level (%)")
    ax1.set_ylim(-2, 102)
    ax1.legend(loc="upper right", fontsize=8, frameon=False)
    ax1.set_title("Tank level under PLC control")

    ax2.step(t, df["pump"], where="post", color="#2da44e", lw=2)
    ax2.set_yticks([0, 1], ["off", "on"])
    ax2.set_ylabel("Pump")
    ax2.set_xlabel("Time (s)")

    for ax in (ax1, ax2):
        ax.spines[["top", "right"]].set_visible(False)

    out = Path(args.csv).with_suffix(".png")
    fig.tight_layout()
    fig.savefig(out, dpi=150)
    print(f"Saved {out}")


if __name__ == "__main__":
    main()
