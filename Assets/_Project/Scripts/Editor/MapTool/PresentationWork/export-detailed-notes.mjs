import fs from 'node:fs/promises';
import {notes,ROOT} from './detailed-deck.mjs';
const out=new URL('./LEAF_AND_LEAP_상세발표_대본과제작보고.txt',import.meta.url);
const seconds=notes.reduce((a,n)=>a+n[2],0);
const CAP=ROOT+'MyGame/Assets/_Project/Scripts/Editor/MapTool/PresentationWork/Captures/';
const resolveImage=f=>f.includes(':/')?f:ROOT+'Docs/'+f;
const imageUses=[
 [18,CAP+'ui-original-request.png','사용자 제공 원본: 참고 이펙트 구현 요청'],
 [18,CAP+'ui-original-result-crop.png','사용자 제공 원본: 구현 결과 발췌'],
 [18,CAP+'ui-original-feedback-crop.png','사용자 제공 원본: 후속 수정 요청 발췌'],
 [8,CAP+'maptool-scene.png','W1_S1 실제 맵툴 그리드·선택 블록 배치 렌더'],
 [8,CAP+'maptool-properties.png','실제 속성·검증 패널 발췌'],
 [14,CAP+'dodge-71.9.png','실제 시연 영상 71.9초: 보스 돌진 직전'],
 [14,CAP+'dodge-72.3.png','실제 시연 영상 72.3초: 회피 발동·잔상'],
 [14,CAP+'dodge-73.2.png','실제 시연 영상 73.2초: 정지 중 이동'],
 [1,'deliverables/stills/01_title.png','실제 타이틀 UI 시연 캡처'],
 [2,'deliverables/stills/02_worldmap.png','실제 월드맵과 노드 정보'],
 [5,'deliverables/stills/03_stage_W1_S1.png','초원 실행 화면'],
 [5,'deliverables/stills/07_stage_W2_S1.png','사막 실행 화면'],
 [5,'deliverables/stills/11_stage_W3_S1.png','설원 실행 화면'],
 [6,'deliverables/stills/15_stage_W4_S1.png','산호 해안 실행 화면'],
 [6,'deliverables/stills/19_stage_W5_S1.png','용암 요새 실행 화면'],
 [11,'art/bosses/GM_BOSS_SproutKing_views.png','새싹왕 모델 다면 뷰'],
 [12,'deliverables/stills/27_boss_W5_B.png','용암 갑룡 순차 분출 시연'],
 [16,'art/v2/revised_T01_before_after.png','초원 지형 모델 v1/v2 비교'],
 [16,'art/worldmap/v3/after_overview.png','v3 월드맵 모델 전체 뷰'],
 [17,'art/bosses/EF_BOSS_EmberIronback_actions.png','용암 갑룡 모델 동작 검토'],
 [19,'deliverables/stills/28_hud.png','HUD 배치'],
 [19,'deliverables/stills/29_pause.png','일시정지 메뉴'],
 [19,'deliverables/stills/30_clear.png','클리어 연출'],
 [19,'deliverables/stills/31_result.png','결과 구획'],
];
for(const [,f] of imageUses) await fs.access(resolveImage(f));
const report=`LEAF & LEAP 개발 결과 발표
22장 상세 발표 대본과 제작 보고
기준일: 2026-11-14
슬라이드: https://chatgpt.com/space/page_10ee2f9565b481919db8509d50b3f65c
형식: 16:9, Noto Sans KR, 남색·금빛·크림
대본 예상 시간: ${Math.floor(seconds/60)}분 ${seconds%60}초
시연 영상: 123.4초 / 영상 포함 약 ${Math.round((seconds+123.4)/60)}분, Q&A 별도
실제 말하는 속도에 따라 발표 시간이 달라집니다.

1. 슬라이드 목록
${notes.map((n,i)=>`${String(i+1).padStart(2,'0')}. ${n[0]} (${n[2]}초)\n    ${n[1]}`).join('\n')}

2. 장별 발표자 대본
${notes.map((n,i)=>`\n[${String(i+1).padStart(2,'0')}] ${n[0]}\n예상 ${n[2]}초\n\n${n[3]}\n\n근거: ${n[4]}\n`).join('\n')}

3. 사용한 이미지 ${imageUses.length}개
이미지는 프로젝트의 실제 실행·편집 화면, 모델 검토 렌더, 사용자가 제공한 원본 대화 캡처입니다. 맵툴은 Unity를 숨김 배치로 실행해 실제 편집 코드를 렌더했고, 퍼펙트 회피는 기존 시연 영상의 원본 프레임을 추출했습니다.
시연 스틸에는 녹화 설명 자막이 포함되어 있습니다. 게임 화면을 새로 생성하거나 바꾸지 않았습니다.
${imageUses.map(([n,f,label])=>`- ${n}장: ${label}\n  ${resolveImage(f)}`).join('\n')}

4. 원래 요청의 수치와 현재 데이터가 다른 항목
- 몬스터: 35개는 CSV 전체 항목 수입니다. Implemented 30개, Planned 5개를 나눠 표기했습니다.
  계획 항목: 1003 점프 순찰병, 1004 원거리 감시자, 1005 도토리 달팽이, 1006 날개콩, 1007 가시밤톨.
- 효과음: 요청서의 19종 대신 현재 SfxBank에 연결된 25개 항목을 사용했습니다.
- 스테이지 계약: 문서 v1.5가 현재 기준이며 JSON version 값은 1을 유지합니다. v1.5는 얼음 surface를 추가합니다.
- 점프 입력 버퍼: 0.1초가 이미 코드에 있어 신규 과제에서 제외하고 향후 튜닝 대상으로 적었습니다. 코요테 타임은 향후 검토입니다.
- 점프 기준: 요청서/W5 설계 노트의 여유 기준과 검증기의 경고 상수(4.0 m/2.6 m)를 구분했습니다. 모든 실제 배치가 더 보수적인 기준을 충족한다고 주장하지 않습니다.
- v2 월드맵 약 68만→289만 폴리곤은 당시 작업 기록의 수치입니다. 현재 메시 재집계나 성능 개선율로 제시하지 않았습니다.
- v3 주요 에셋 2~3만 tris는 아트 계약의 목표 기준입니다. 모든 에셋의 현재 실측값으로 제시하지 않았습니다.
- UI 작업의 GPT Astra 사용을 18·20장에 명시했습니다. 모델명은 사용자의 이번 확인에 근거하며, Claude와 MCP의 역할은 대화·프로젝트 기록을 함께 참조했습니다. 세부 모델 버전이나 도구별 기여율은 임의로 만들지 않았습니다.
- 타이틀 Cubism 호흡 OFF 유지 상태를 반영했습니다. 새 리깅 또는 호흡 애니메이션을 완료했다고 표현하지 않았습니다.

5. 확인하지 못해 확정 수치를 넣지 않은 항목
- 25개 전체 스테이지의 최신 Unity 검증 실행 원본 로그.
- 보스 5종의 전체 페이즈 완주 자동 테스트 원본 결과. 시연 로그로 확인한 것은 공격·기절·HP 3→2입니다.
- 플레이어 빌드와 전체 런타임 테스트. 이번 캡처용 Unity 편집기 배치는 컴파일 및 실행에 성공했지만, 플레이어 빌드·완주·성능 검증까지 수행한 것은 아닙니다.
- 목표 기기, 목표 FPS, CPU/GPU 프레임 시간, 메모리와 드로 콜 예산.
- 사람의 완주율, 사망 횟수, 클리어 시간 분포와 난이도 체감.
- 실제 총 개발 투입 시간. 타임라인의 날짜를 작업 시간으로 환산하지 않았습니다.
- 용암 기믹의 최초 원문 대화는 제공된 이전 대화 기록에서 확보하지 못했습니다. 직접 제안했다는 귀속은 이번 사용자 요청서의 명시에 근거합니다.
이 항목들은 0 또는 통과로 채우지 않았습니다. 기존 소개서의 검증 보고는 '기존 보고'로 남겼습니다.

6. 추가 캡처 반영 내역
- 8장: W1_S1의 실제 StageMapToolWindow와 SceneView를 데스크톱 창 표시·키보드·마우스 입력 없이 렌더했습니다.
  선택 블록: W1_S1_B_ground1 (x=20, y=1, w=3, h=11).
  0.5 m 그리드와 표면 스냅을 사용합니다. 해당 JSON 검증 결과 오류 0, 경고 0입니다.
  전체 패널 원본: ${CAP}maptool-actual.png / 슬라이드에서는 속성·검증 영역을 발췌했습니다.
  검증 근거: ${CAP}maptool-validation.txt / 실행 로그: ${CAP}maptool-batch.log.
- 14장: LeafAndLeap_Showcase.mp4의 71.9초·72.3초·73.2초를 원본 해상도로 추출했습니다.
  영상의 색상·흑백 전환은 원래 게임 연출입니다. 보스가 멈춘 상태에서 플레이어 위치가 바뀌는 것을 확인했습니다.
- 8장과 14장의 캡처 자리 표시를 모두 교체했습니다.
- 18장은 UI 내부 구조 설명을 GPT Astra로 클리어 UI를 제작·수정한 과정으로 전면 교체했습니다.
  사용자가 제공한 원본 대화 캡처 2장을 사용했습니다. 요청, 구현 결과, 후속 수정 요청의 3개 영역으로 나누었습니다.
  원본: ${CAP}ui-original-request.png / ${CAP}ui-original-result-feedback.png.
  이미지의 대화 내용·날짜·색상은 변경하지 않았고, 필요한 부분만 크롭했습니다. 이전 HTML 대화 캡처는 최종 슬라이드에서 제외했습니다.
  캡처 속 과거 AI 검증 보고를 새로 검증한 사실로 인용하지 않으며, 현재 결과는 19장의 실제 게임 캡처와 구분해 설명합니다.
선택 보강용 장면(이번 발표를 완성하는 데 필수 아님)
- 보스 기절 직후 컷인·약점 밟기, 자막 없는 UI, 무적을 끈 W5 분출 회피와 사람의 완주 영상.

7. 시연 안내와 발표 전 준비
영상: ${ROOT}Docs/deliverables/LeafAndLeap_Showcase.mp4
22장에 도달하면 위 영상을 별도 플레이어로 재생합니다. 슬라이드에 영상 파일 자체는 내장하지 않았습니다.
일반 스테이지, 보스, UI 순서의 기능 소개 영상이며 자동 입력·무적 설정을 사용합니다.
31_result.png의 0.87초는 시연용 결과값이므로 정상 완주 기록으로 설명하지 않습니다.
슬라이드는 뷰어에서 .pptx로 내려받을 수 있습니다. 다운로드 뒤 발표 기기에서 Noto Sans 표시와 동영상 재생을 확인해 주세요.
유니티짱 표기: © Unity Technologies Japan/UCL (표지와 마지막 장)

8. 검토 범위
소스 코드·JSON·CSV·에셋 및 지정 문서를 대조했습니다.
슬라이드를 PNG로 렌더해 한 장씩 확인하고, 표와 글자 배치 및 이미지 비율을 점검했습니다.
이 검토는 게임의 최신 완주 테스트나 PowerPoint 실기 실행 검증을 뜻하지 않습니다.
컴퓨터 화면 조작은 하지 않았습니다. 게임 플레이 코드는 변경하지 않았으며, 명시적으로 실행할 때만 작동하는 발표 캡처용 편집기 보조 코드를 추가했습니다.
`;
await fs.writeFile(out,'\ufeff'+report,'utf8');
console.log(JSON.stringify({slides:notes.length,speakerSeconds:seconds,images:imageUses.length,file:out.pathname}));
