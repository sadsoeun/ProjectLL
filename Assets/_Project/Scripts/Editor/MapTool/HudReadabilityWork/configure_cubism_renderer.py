from pathlib import Path
import shutil
work=Path(__file__).resolve().parent
project=work.parents[5]
guid='1f003999a57a1da40950cb24efcbacdc'
for name in ['PC_RPAsset','Mobile_RPAsset']:
    path=project/'Assets/Settings'/(name+'.asset')
    backup=work/'Backup'/(name+'.asset.txt')
    if not backup.exists():shutil.copy2(path,backup)
    text=path.read_text(encoding='utf-8-sig')
    if guid not in text:
        block=text.split('  m_RendererDataList:\n')[1].split('  m_DefaultRendererIndex:')[0]
        assert block.count('fileID:')==1,block
        text=text.replace('  m_DefaultRendererIndex:',f'  - {{fileID: 11400000, guid: {guid}, type: 2}}\n  m_DefaultRendererIndex:',1)
        path.write_text(text,encoding='utf-8')
shutil.copy2(work/'Staged/TitleLive2DCharacter.cs.txt',project/'Assets/_Project/Scripts/Title/TitleLive2DCharacter.cs')
print('Dedicated Cubism renderer added at index 1; gameplay renderer remains index 0.')
