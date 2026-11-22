import ast,json,re
from pathlib import Path
root=Path(__file__).parent
previous=json.loads((root/'plain-language-prepared.json').read_text(encoding='utf-8'))
# Restore design vocabulary selectively; retain the explanations of implementation details.
restore={1:[0,1],2:[0],3:[1,4,7,8],4:[0,1,3,4],5:[1,3],6:[2],13:[0,8],14:[0,2,3],15:[0,2,9,10],16:[3],18:[0,1],19:[0,2,3],20:[0],21:[0,8]}
changes={p['slide']:[[p['replacements'][i][1],p['replacements'][i][0]] for i in restore.get(p['slide'],[])] for p in previous}
extra={
3:[['설원~용암 정리\n1차 개발 정리','설원~용암 정리\n1차 마일스톤'],['만화풍 아트','툰 아트']],
4:[['길이: 초원 1-1~1-4 배치 파일 / 학습 순서: 맵 설계 문서의 의도','길이: 초원 1-1~1-4 배치 파일 / 학습 순서: 레벨 디자인 문서의 의도']],
7:[['점프할 수 있는 거리와 높이 확인','점프 설계 기준과 검증']],
9:[['몬스터의 행동과 능력치를 나눈 이유','몬스터 AI와 능력치 데이터의 분리']],
11:[['체력 3 / 공격 구성 3단계','체력 3 / 3개 페이즈'],['기절 중 약점 밟기 → 체력 1 감소\n체력 3 / 공격 구성 3단계','기절 중 약점 밟기 → 체력 1 감소\n체력 3 / 3개 페이즈']],
13:[['착지 직전 입력 기억','점프 입력 버퍼'],['추후 검토: 발판을 벗어난 직후에도 잠깐 점프를 허용하는 기능','추후 검토: 코요테 타임 — 발판을 벗어난 직후에도 잠깐 점프를 허용']],
15:[['특별 수집품 3개, 기록·신기록\n부활 지점·목숨·다음 스테이지 열기','특별 수집품 3개, 기록·신기록\n체크포인트·목숨·다음 스테이지 개방']],
16:[['만화풍 색감·거리별 모델','툰 아트·거리별 모델 단순화']],
21:[['사람의 완주·용암 타이밍 / 발판 끝 점프·입력 여유 조정\n지역별 배경음악 / 사용할 기기·목표 실행 속도','플레이 테스트·용암 타이밍 / 코요테 타임·입력 버퍼 조정\n월드별 BGM / 목표 플랫폼·실행 성능']]
}
# Whole text values only; the phase fragment above is covered by its full text.
extra[11]=extra[11][1:]
# Use the original concise art heading, without restoring its version prefix.
changes[3]=[p for p in changes[3] if p[0]!='만화풍 아트']
for n,pairs in extra.items():changes[n].extend(pairs)
changes=[{'slide':n,'replacements':p} for n,p in changes.items() if p]
path=root/'detailed-deck.mjs';source=path.read_text(encoding='utf-8')
a=source.index('export const notes=');b=source.index('\nfunction text(',a)
notes=json.loads(source[a+len('export const notes='):b].strip().rstrip(';'))
for c in changes:
 n=c['slide'];m=dict(c['replacements'])
 for i in (0,1):notes[n-1][i]=m.get(notes[n-1][i],notes[n-1][i])
 a=source.index(' if(n==='+str(n)+'){');b=source.index('\n if(n===',a) if n<22 else source.index('\n if(input.replace)',a)
 def repl(match):
  token=match.group(0)
  try:value=ast.literal_eval(token)
  except (ValueError,SyntaxError):return token
  return json.dumps(m[value],ensure_ascii=False) if value in m else token
 source=source[:a]+re.sub(r'''"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*' '''.strip(),repl,source[a:b])+source[b:]
a=source.index('export const notes=');b=source.index('\nfunction text(',a)
source=source[:a]+'export const notes='+json.dumps(notes,ensure_ascii=False,indent=2)+';\n'+source[b:]
path.write_text(source,encoding='utf-8')
(root/'planning-terms-changes.json').write_text(json.dumps(changes,ensure_ascii=False,indent=2),encoding='utf-8')
script_path=root/'presentation-script.json';script=json.loads(script_path.read_text(encoding='utf-8'))
script[0]['speech']=script[0]['speech'].replace('3D 캐릭터로 다섯 지역을 지나며, 옆으로 이동하고 점프와 회피로 길을 돌파하는 게임입니다.','다섯 월드를 지나며 점프와 회피로 길을 돌파하는 3D 횡스크롤 액션 플랫포머입니다.')
script[2]['transition']='그중 게임의 기본 경험을 결정한 부분이 레벨 디자인입니다.'
script[3]['key']='기본 조작 학습부터 기믹 조합까지 이어지는 학습 곡선'
script[3]['speech']=script[3]['speech'].replace('초원에서는 플레이어가 조작을 하나씩 익히게 하고 싶었습니다.','초원 레벨 디자인에서는 플레이어가 조작을 하나씩 익히는 학습 곡선을 만들고 싶었습니다.').replace('다음 맵부터 높이 조절과 가시, 움직이는 발판을 만나도록 했습니다.','다음 맵부터 가변 점프와 가시, 움직이는 발판을 만나도록 했습니다.').replace('AI로 맵을 만들 때도','AI로 레벨을 만들 때도').replace('같은 조작을 새로운 상황에서 다시 사용하게 한다는 점입니다.','익힌 조작을 새로운 기믹과 조합해 사용하게 한다는 점입니다.')
script[4]['speech']=script[4]['speech'].replace('지역별 배경·소품과 맵 구성을','테마별 리소스와 맵 구성을').replace('장애물과 장치가','기믹이')
script[5]['transition']='이런 동선을 만들려면 우선 캐릭터가 실제로 건널 수 있는지 확인해야 합니다.'
script[8]['speech']=script[8]['speech'].replace('몬스터를 만들 때는 무엇을 할지와 얼마나 빠르고 강할지를 나눴습니다.','몬스터는 AI의 행동 흐름과 능력치 데이터를 나눠 설계했습니다. 무엇을 할지와 얼마나 빠르고 강할지를 따로 조정하는 방식입니다.')
script[10]['speech']=script[10]['speech'].replace('체력이 줄어들면 다음 단계의 공격 조합으로 바뀝니다.','체력이 줄어들면 다음 페이즈로 넘어가며 공격 조합이 바뀝니다.')
script[12]['speech']=script[12]['speech'].replace('점프 버튼을 누르는 길이에 따라 높이도 조절할 수 있습니다.','가변 점프는 버튼을 누르는 길이에 따라 점프 높이가 달라지도록 했습니다.').replace('또 착지하기 직전, 0.1초 먼저 누른 점프를 잠깐 기억했다가 착지하면 실행합니다.','점프 입력 버퍼도 적용했습니다. 착지하기 직전, 0.1초 먼저 누른 점프를 잠깐 기억했다가 착지하면 실행합니다.').replace('앞으로는 발판 끝을 벗어난 직후에도 잠깐 점프를 허용하는 기능을 검토하려고 합니다.','앞으로는 코요테 타임, 즉 발판 끝을 벗어난 직후에도 잠깐 점프를 허용하는 기능을 검토하려고 합니다.')
script[13]['speech']=script[13]['speech'].replace('지나간 자리에 남는 잔상','대시 잔상')
script[14]['speech']=script[14]['speech'].replace('죽었을 때 다시 시작할 지점과 저장 기능은','체크포인트와 저장 기능은')
script[15]['speech']=script[15]['speech'].replace('모델의 모습은 세 단계를 거쳐 다듬었습니다.','아트는 세 단계를 거쳐 다듬었습니다.').replace('만화 같은 색감을 맞추고','툰 아트의 색감을 맞추고')
script[17]['speech']=script[17]['speech'].replace('참고 링크를 주고','레퍼런스 링크를 주고')
script[18]['speech']=script[18]['speech'].replace('플레이 중 표시되는 정보에서는 목숨과 수집 정보, 시간을 나누고','HUD에서는 목숨과 수집 정보, 시간을 나누고')
script[20]['speech']=script[20]['speech'].replace('점프 입력의 여유를 조정하려고 합니다.','입력 버퍼를 조정하려고 합니다.').replace('지역별 배경음악과, 어떤 기기에서 얼마나 부드럽게 실행할지도 기준을 정할 예정입니다.','월드별 BGM과 목표 플랫폼, 실행 성능의 기준도 정할 예정입니다.')
script_path.write_text(json.dumps(script,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'slidesAdjusted':len(changes),'replacements':sum(len(c['replacements']) for c in changes)}))
