"""DRM and RSS counters for the owned renderer process."""
from pathlib import Path
import time


class MemorySampler:
    """Owned process only; deduplicate duplicated DRM descriptors by device/client."""
    def __init__(self, pid):
        self.root = Path('/proc') / str(pid)
        clients = {}
        for path in (self.root / 'fdinfo').iterdir():
            try:
                fields = self.fields(path)
            except FileNotFoundError:
                continue  # An unrelated descriptor can close during enumeration.
            if 'drm-client-id' in fields:
                clients.setdefault((fields.get('drm-pdev'), fields['drm-client-id']), path)
        self.paths = list(clients.values())
        if not self.paths:
            raise RuntimeError('Owned desktop has no DRM memory counters')

    @staticmethod
    def fields(path):
        return {key: value.strip() for line in path.read_text().splitlines()
                if ':' in line for key, value in [line.split(':', 1)]}

    @staticmethod
    def size(value):
        parts = value.split()
        return int(parts[0]) * ({'KiB': 1024, 'MiB': 1024**2, 'kB': 1024}.get(parts[1], 1) if len(parts) > 1 else 1)

    def sample(self):
        before = time.monotonic_ns()
        counters = {}
        for path in self.paths:
            for key, value in self.fields(path).items():
                if key.startswith(('drm-resident-', 'drm-total-', 'drm-shared-')):
                    counters[key] = counters.get(key, 0) + self.size(value)
        return {'atNs': before, 'clients': len(self.paths), 'drmBytes': counters,
                'residentBytes': sum(v for k, v in counters.items() if k.startswith('drm-resident-')),
                'requestedBytes': sum(v for k, v in counters.items() if k.startswith('drm-total-')),
                'rssBytes': self.size(self.fields(self.root / 'status')['VmRSS']),
                'sampleMs': (time.monotonic_ns() - before) / 1e6}
