from pathlib import Path
import re,subprocess
root=Path('C:/Users/PC/UnityStudy/MyGame')
out=Path('C:/Users/PC/.codex/visualizations/2026/10/31/01a19252-8ac5-7110-b2a9-856eb2b4ca42/ui-upgrade-compile')
out.mkdir(exist_ok=True)
editor=Path('C:/Program Files/Unity/Hub/Editor/6000.3.6f1/Editor/Data')
for assembly in ['Assembly-CSharp','Assembly-CSharp-Editor']:
    original=(root/'Library/Bee/artifacts/1900b0aE.dag'/f'{assembly}.rsp').read_text(encoding='utf-8-sig')
    text=re.sub(r'^-out:.*$',lambda _:f'-out:"{out}/{assembly}.dll"',original,flags=re.M)
    text=re.sub(r'^-refout:.*$',lambda _:f'-refout:"{out}/{assembly}.ref.dll"',text,flags=re.M)
    if assembly.endswith('Editor'):
        text=text.replace('-r:"Library/Bee/artifacts/1900b0aE.dag/Assembly-CSharp.ref.dll"',f'-r:"{out}/Assembly-CSharp.ref.dll"')
        source='"Assets/_Project/Scripts/Editor/MapTool/UiUpgradeVerification.cs"'
        if source not in text:text+='\n'+source+'\n'
    rsp=out/(assembly+'.rsp');rsp.write_text(text,encoding='utf-8')
    result=subprocess.run([str(editor/'NetCoreRuntime/dotnet.exe'),str(editor/'DotNetSdkRoslyn/csc.dll'),'@'+str(rsp)],cwd=root,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True,encoding='utf-8',errors='replace')
    (out/(assembly+'.log')).write_text(result.stdout,encoding='utf-8')
    print(assembly,'exit',result.returncode)
    print('\n'.join(x for x in result.stdout.splitlines() if 'error ' in x)[:7000])
    print('Warnings:',sum('warning ' in x for x in result.stdout.splitlines()),'(details in log)')
    if result.returncode:raise SystemExit(result.returncode)
