# 로컬 Stable Diffusion 설치 가이드 (Windows, 초보용)

> 집 PC의 VS Code Claude에게 "docs/art/SD_SETUP.md 따라 설치 도와줘"라고 하면 1단계 확인부터 같이 진행할 수 있습니다.

## 1. 내 PC로 되는지 확인
VS Code 터미널(또는 명령 프롬프트)에서:
```bash
nvidia-smi
```
| 결과 | 판단 |
|---|---|
| GPU 이름 + 메모리 **12GB 이상** | 최적. SDXL 생성 + 캐릭터 LoRA 학습까지 가능 |
| **8GB** | SDXL 생성 가능(조금 느림). LoRA 학습은 설정을 낮춰야 함 |
| 6GB 이하 | SD1.5 계열만 현실적 |
| 명령을 찾을 수 없음 | NVIDIA 그래픽카드가 아님 → AMD/인텔은 설치가 까다로움, Claude와 따로 상의 |

- 디스크 여유 공간 **50GB 이상** (모델 1개당 2~7GB)

## 2. 설치 — Stability Matrix (추천, 원클릭 관리 프로그램)
1. https://lykos.ai 또는 GitHub `LykosAI/StabilityMatrix` 의 Releases 에서 Windows용 zip 다운로드
2. 압축 풀고 `StabilityMatrix.exe` 실행 → 데이터 폴더 위치 지정(용량 큰 드라이브 권장, 경로에 한글 없는 곳: 예 `D:\AI\StabilityMatrix`)
3. **Packages → Add Package**에서 하나 선택해 설치
   - **ComfyUI** (추천): 노드 방식. 포즈 고정(ControlNet)·배경 제거·반복 작업 자동화에 유리 → 장비 부위 대량 생성에 적합
   - **Stable Diffusion WebUI Forge**: 버튼식 화면, 처음 쓰기 쉬움
4. 설치 후 **Launch** → 브라우저에 화면이 뜨면 성공

## 3. 우리 프로젝트에 필요한 추가 기능
| 기능 | 용도 |
|---|---|
| ControlNet (OpenPose, Lineart) | 모든 장비 부위를 **같은 포즈**로 생성 → 옷 갈아입히기 겹침 맞춤 |
| 배경 제거 (rembg 계열) | 투명 PNG |
| LoRA 학습 도구 (kohya_ss 등, Stability Matrix 패키지에 있음) | 리아·동료 얼굴/체형 고정 |

## 4. 모델 고르기 — ⚠️ 상업 이용 확인 필수
- 베이스: SDXL 계열 애니풍 모델 (Civitai, Hugging Face)
- 다운로드 전에 모델 페이지의 **라이선스/허용 범위** 확인: 상업적 이미지 판매, 상업 서비스 사용 허용 여부, 매출 한도 조건
- 확인한 내용은 `docs/art/LICENSES.md`에 기록 (모델명, 버전, 링크, 라이선스, 확인 날짜)
- ⚠️ IP 경고: 기존 게임·애니 **캐릭터 LoRA**, **실존 작가명 프롬프트**, 레퍼런스 게임 이미지를 img2img 입력으로 쓰기 → 금지

## 5. 콘텐츠 규칙 (CLAUDE.md와 동일)
- 모든 캐릭터는 성인 외형. SD(3등신) 이미지에 선정적 연출 금지
- 스토어판 / 성인판 결과물은 폴더를 나눠 저장 (`Art/Store`, `Art/Adult`)
