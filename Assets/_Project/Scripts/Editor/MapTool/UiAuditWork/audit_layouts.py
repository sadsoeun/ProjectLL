import re,json,hashlib,math
from pathlib import Path
ROOT=Path('C:/Users/PC/UnityStudy/MyGame')
OUT=Path(__file__).parent

def parse(path):
    docs={}
    for m in re.finditer(r'^--- !u!(\d+) &(-?\d+)[^\n]*\n(.*?)(?=^--- !u!|\Z)',path.read_text(encoding='utf-8-sig'),re.M|re.S):
        typ,fid,body=m.groups(); docs[fid]=(int(typ),body)
    gos={fid:m.group(1) for fid,(typ,body) in docs.items() if typ==1 and (m:=re.search(r'^  m_Name: (.*)$',body,re.M))}
    transforms={}
    for fid,(typ,body) in docs.items():
        if typ not in (4,224):continue
        go=re.search(r'm_GameObject: \{fileID: (-?\d+)',body)
        par=re.search(r'm_Father: \{fileID: (-?\d+)',body)
        transforms[fid]=(go.group(1) if go else '',par.group(1) if par else '0')
    def pathof(fid):
        if fid not in transforms:return ''
        go,par=transforms[fid]
        return (pathof(par)+'/' if par!='0' else '')+gos.get(go,'?')
    go_paths={go:pathof(fid) for fid,(go,par) in transforms.items()}
    rows=[]
    for fid,(typ,body) in docs.items():
        if typ!=224:continue
        fields={}
        for key in ['m_AnchorMin','m_AnchorMax','m_AnchoredPosition','m_SizeDelta','m_Pivot']:
            m=re.search(key+r': \{x: ([^,]+), y: ([^}]+)',body)
            fields[key]=[float(x) for x in m.groups()] if m else None
        rows.append({'path':pathof(fid),**fields})
    return rows,docs,go_paths

summary=[]; details={}
for path in sorted((ROOT/'Assets/_Project/Scenes').rglob('*.unity')):
    if 'Tests' in path.parts or path.name.startswith('Test_'):continue
    rows,docs,go_paths=parse(path)
    hud=[r for r in rows if r['path'].startswith('HUDCanvas/')]
    digest=hashlib.sha256(json.dumps(sorted(hud,key=lambda r:r['path']),sort_keys=True).encode()).hexdigest()[:12] if hud else None
    summary.append({'scene':path.name,'rects':len(rows),'hud_rects':len(hud),'hud_layout_hash':digest})
    details[path.name]=rows
for path in sorted((ROOT/'Assets/_Project/Prefabs/UI').glob('*.prefab')):
    rows,_,_=parse(path); details[path.name]=rows
(OUT/'layout-inventory.json').write_text(json.dumps({'scenes':summary,'layouts':details},ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(summary,indent=2))
for name, suffixes in [('02_LanguageSelect.unity',['LanguageList','Lang_ja','ConfirmButton','ErrorText']),('SettingsPopup.prefab',['Window']),('06_InGame.unity',['HUDCanvas','PauseMenu/Window','GameOverPanel/Window'])]:
    print(name)
    for r in details[name]:
        if any(r['path'].endswith(s) for s in suffixes):print(r)
scale=math.sqrt(2560/1920*1080/1080); height=1080/scale
print('2560x1080 canvas height:',height,'last language bottom:',220+4*124+100,'confirm top:',height-90-100,'overlap UI units:',220+4*124+100-(height-90-100))
