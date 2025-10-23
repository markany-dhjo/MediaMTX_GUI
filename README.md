# MediaMTX GUI for RTSP Streaming

.NET 8 WPF 애플리케이션으로 동영상 파일을 MediaMTX를 통해 RTSP 스트리밍합니다.

## 필요 사항

1. **MediaMTX**: `refs` 디렉토리에 `mediamtx.exe` 배치
2. **FFmpeg**: `refs` 디렉토리에 `ffmpeg.exe` 배치

## 주요 기능

- **다중 파일 선택**: 여러 동영상 파일 동시 스트리밍 지원
- **파일 목록 관리**: 개별 파일 삭제, 전체 목록 지우기
- **설정 저장**: 선택한 파일 목록이 프로그램 재시작 후에도 유지
- **스트리밍 설정**: 해상도 및 FPS 조정 가능
- **자동 설정**: MediaMTX 설정 파일 자동 생성
- **실시간 로그**: 스트리밍 상태 및 오류 실시간 출력
- **프로세스 관리**: 기존 MediaMTX 프로세스 자동 정리

## 사용법

1. **파일 선택**: "📁 파일 선택" 버튼으로 스트리밍할 동영상들 선택
2. **스트리밍 설정**: 해상도와 FPS 조정 (선택사항)
3. **스트리밍 시작**: "▶ 시작" 버튼 클릭
4. **RTSP URL 확인**: 
   - Stream 1: `rtsp://localhost:8554/stream1`
   - Stream 2: `rtsp://localhost:8554/stream2`
   - 외부 접속: `rtsp://[IP주소]:8554/stream[번호]`
5. **스트리밍 중지**: "⏹ 중지" 버튼 클릭

## 지원 형식

- 동영상: mp4, avi, mkv, mov
- 해상도: 원본, 1920x1080, 1280x720, 854x480, 640x360
- FPS: 원본, 60, 30, 24, 15

## 빌드 및 실행

```bash
dotnet build
dotnet run
```

## 파일 구조

```
MediaMTX_GUI/
├── MediaMTX_GUI.csproj
├── MainWindow.xaml
├── MainWindow.xaml.cs
├── App.xaml
├── App.xaml.cs
├── refs/
│   ├── mediamtx.exe
│   └── ffmpeg.exe
└── README.md
```

## 자동 기능

- **빌드시 파일 복사**: `refs` 디렉토리의 실행 파일들이 빌드 출력 디렉토리로 자동 복사
- **프로세스 정리**: 프로그램 시작시 기존 MediaMTX 프로세스 자동 종료
- **설정 저장**: 선택한 파일 목록이 `settings.txt`에 자동 저장/복원
