"""Bounded native HLS probe; no stream is recorded and no signatures are generated.

Usage: python probe_stream.py PRIVATE_CONFIG OUTPUT_JSON [--seconds 20]
URLs must come from an ordinary, authorized official playback session.
"""
import argparse
import array
import datetime
import json
import math
import time
from pathlib import Path

import av
import requests


def probe(item, seconds):
    start = time.monotonic()
    result = {"channel": item["name"], "tested_at": datetime.datetime.now(datetime.timezone.utc).isoformat(),
              "requested_media_seconds": seconds, "headers_used": list(item.get("headers", {})),
              "decoded_frames": {"video": 0, "audio": 0}}
    headers = item.get("headers", {})
    try:
        response = requests.get(item["url"], headers=headers, timeout=15)
        result["manifest_status"] = response.status_code
        result["cors_origin"] = response.headers.get("Access-Control-Allow-Origin")
        result["manifest_is_hls"] = response.text.startswith("#EXTM3U")
        result["manifest_has_key_tag"] = "#EXT-X-KEY" in response.text
        result["segment_durations"] = [float(x.split(":", 1)[1].split(",")[0]) for x in response.text.splitlines() if x.startswith("#EXTINF:")]
        response.raise_for_status()
        options = {"headers": "".join(f"{k}: {v}\r\n" for k, v in headers.items())} if headers else {}
        sample_count, square_sum, peak = 0, 0, 0
        first_pts, last_pts = {}, {}
        resampler = av.AudioResampler(format="s16", layout="mono", rate=16000)
        with av.open(item["url"], options=options, timeout=(15, 15)) as container:
            result["streams"] = [{"type": s.type, "codec": s.codec_context.name} for s in container.streams]
            decode_start = time.monotonic()
            for frame in container.decode():
                kind = "video" if isinstance(frame, av.VideoFrame) else "audio"
                result["decoded_frames"][kind] += 1
                result.setdefault("first_frame_seconds", {}).setdefault(kind, round(time.monotonic() - start, 3))
                if frame.time is not None:
                    first_pts.setdefault(kind, frame.time)
                    last_pts[kind] = frame.time
                if kind == "video":
                    result["resolution"] = [frame.width, frame.height]
                else:
                    for pcm in resampler.resample(frame):
                        values = array.array("h", bytes(pcm.planes[0])[:pcm.samples * 2])
                        sample_count += len(values)
                        square_sum += sum(v * v for v in values)
                        peak = max(peak, max((abs(v) for v in values), default=0))
                spans = {k: last_pts[k] - first_pts[k] for k in last_pts}
                if spans.get("video", 0) >= seconds and spans.get("audio", 0) >= seconds:
                    break
                if time.monotonic() - decode_start > seconds + 40:
                    result["bounded_timeout"] = True
                    break
            result["media_span_seconds"] = {k: round(v, 3) for k, v in spans.items()}
        result["audio_peak_s16"] = peak
        result["audio_rms_s16"] = round(math.sqrt(square_sum / sample_count), 2) if sample_count else None
        result["passed"] = (result["media_span_seconds"].get("video", 0) >= seconds and
                            result["media_span_seconds"].get("audio", 0) >= seconds and peak > 0)
    except Exception as exc:
        # Exceptions may contain signed URLs: do not leak them into the report.
        result["error_type"] = type(exc).__name__
        result["passed"] = False
    result["wall_seconds"] = round(time.monotonic() - start, 3)
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("config")
    parser.add_argument("output")
    parser.add_argument("--seconds", type=float, default=20)
    args = parser.parse_args()
    results = []
    for item in json.loads(Path(args.config).read_text()):
        result = probe(item, args.seconds)
        results.append(result)
        Path(args.output).write_text(json.dumps(results, ensure_ascii=False, indent=2))
        print(json.dumps(result, ensure_ascii=False), flush=True)
