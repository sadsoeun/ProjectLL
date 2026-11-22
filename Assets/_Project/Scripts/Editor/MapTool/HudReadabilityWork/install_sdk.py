from pathlib import Path
import tarfile
project=Path(r'C:\Users\PC\UnityStudy\MyGame')
package=Path(__file__).parent/'CubismSdkForUnity-5-r.6.1.unitypackage'
destination=project/'Assets/Live2D'
assert not destination.exists(), 'SDK already exists; review before overwrite'
count=0
with tarfile.open(package) as archive:
    entries={m.name:m for m in archive.getmembers()}
    for name,member in entries.items():
        if not name.endswith('/pathname'):continue
        path=archive.extractfile(member).read().decode('utf-8').strip()
        if not path.startswith('Assets/Live2D') or '/Samples' in path:continue
        target=(project/path).resolve()
        assert target==destination or destination in target.parents
        prefix=name.rsplit('/',1)[0]
        asset=entries.get(prefix+'/asset');meta=entries.get(prefix+'/asset.meta')
        if asset:
            target.parent.mkdir(parents=True,exist_ok=True)
            target.write_bytes(archive.extractfile(asset).read());count+=1
        else:target.mkdir(parents=True,exist_ok=True)
        if meta:Path(str(target)+'.meta').write_bytes(archive.extractfile(meta).read())
print('Installed official Cubism Unity URP SDK without sample content:',count,'files')
