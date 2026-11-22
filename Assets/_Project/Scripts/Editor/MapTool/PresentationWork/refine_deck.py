from pathlib import Path
p=Path(__file__).with_name('detailed-deck.mjs')
s=p.read_text(encoding='utf-8')
s=s.replace("['초원·사막·설원의 기믹'", "['다섯 월드의 테마와 기믹'")
s=s.replace('Docs/design/W1-level-notes.md; ', '')
s=s.replace('Docs/design/W1-level-notes.md, W2-level-notes.md, W3-level-notes.md', 'Docs/level-design-report.pdf; Docs/design/W2_S1-S3-level-notes.md, W2_S4-bosses-level-notes.md, W3-level-notes.md')
s=s.replace('Editor/ShowcaseDirector.cs; DemoMode.cs','Dev/ShowcaseDirector.cs; Core/DemoMode.cs')
s=s.replace('키보드와 게임패드를 지원하며, 언어는', '월드맵에서는 노드 정보와 메뉴, 휠 확대·축소를 사용할 수 있습니다. 키보드와 게임패드를 지원하며, 언어는')
s=s.replace('물결과 바람, 용암 맥동 같은 환경 움직임도 추가했습니다.', '스위치에는 유지형과 해제 뒤 2초 유지형을 두고, 게이트와 빙판, 이동 발판, 낙하물을 조합했습니다. 물결과 바람, 용암 맥동 같은 환경 움직임도 추가했습니다.')
s=s.replace('모델링에서는 Blender 원본과', '작업 기록의 모델링 도구는 Blender 5.2입니다. Blender 원본과')
s=s.replace('큰 색 변화와 변형 모듈을 사용해 완화했습니다.', '매크로 색 변화와 변형 모듈, 좌우 반전을 사용해 완화했습니다.')
s=s.replace("for(const n of input.slides??[]){let s=base(p,n);", "for(const n of input.slides??[]){if(input.replace)p.slides.getItem(n-1).delete();let s=base(p,n);")
s=s.replace(" const a=notes[n-1];", " if(input.replace)s.moveTo(n-1);\n const a=notes[n-1];")
s=s.replace("'Blender + MCP / Python 빌드'", "'Blender 5.2 + MCP / Python 빌드'")
s=s.replace("text(s,'용암 바다·스위치 철창 / 순서와 타이밍',656,594,568,42,25)","text(s,'용암 바다·스위치 철창 / 순서와 타이밍',656,594,568,42,23)")
lines=s.splitlines()
for i,line in enumerate(lines):
 if line.startswith(' if(n===5)'):
  lines[i]=""" if(n===5){const imgs=['03_stage_W1_S1.png','07_stage_W2_S1.png','11_stage_W3_S1.png'];for(let i=0;i<3;i++)img(s,STILL+imgs[i],56+i*397,193,374,192);table(s,[['월드','핵심 요소','플레이 판단'],['W1 초원','기본 점프·밟기','경로와 착지 익히기'],['W2 사막','스위치 문·타이밍 가시','관찰 후 출발하기'],['W3 설원','빙판·추격형 적·얼음 다리','제동과 착지 조절'],['W4 산호 해안','이동 뗏목·잔교','기다렸다 건너기'],['W5 용암 요새','용암 바다·스위치 철창','순서와 타이밍 조합']],56,405,1168,240,[220,490,458],20);footer(s,'위: W1·W2·W3 실제 실행 화면 / 다음 장: W4·W5 동선 / 각 월드 일반 4개 + 보스 1개');}"""
 if line.startswith(' if(n===19)'):
  lines[i]=""" if(n===19){const fs=['28_hud.png','29_pause.png','30_clear.png'],labels=['HUD','일시정지','클리어 연출'],desc=['목숨·수집·시간 분리','현재 행동과 복귀 경로','축하 후 결과 공간 확보'];for(let i=0;i<3;i++){const y=205+i*147;img(s,STILL+fs[i],56,y,224,126);text(s,labels[i],303,y+7,240,35,24,C.gold);text(s,desc[i],303,y+47,240,70,21);}img(s,STILL+'31_result.png',576,205,648,365);text(s,'시간 / 코인 / 특별 수집품',576,596,648,42,29,C.gold);footer(s,'실제 시연 캡처 / 타이틀: 1장, 월드맵: 2장 / 결과의 0.87초는 자동 시연용 수치');}"""
s='\n'.join(lines)+'\n'
p.write_text(s,encoding='utf-8')
print('Updated slide content and notes')
