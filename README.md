# MediaMTX GUI for RTSP Streaming

.NET 8 WPF 애플리케이션으로 동영상 파일을 MediaMTX를 통해 RTSP 스트리밍합니다.

## 필요 사항

1. **MediaMTX**: 실행 파일 디렉토리에 `mediamtx.exe` 배치
2. **FFmpeg**: 실행 파일 디렉토리에 `ffmpeg.exe` 배치

## 사용법

1. "동영상 파일 선택" 버튼으로 스트리밍할 동영상 선택
2. "시작" 버튼 클릭하여 RTSP 스트리밍 시작
3. RTSP URL: `rtsp://localhost:8554/stream`
    - 외부에서 접속할 경우 URL : `rtsp://[IP주소]:8554/stream`
4. "중지" 버튼으로 스트리밍 중단

## 빌드 및 실행

```bash
dotnet build
dotnet run
```

## 주요 기능

- 동영상 파일 선택 (mp4, avi, mkv, mov)
- 자동 MediaMTX 설정 파일 생성
- 실시간 로그 출력
- RTSP 스트리밍 시작/중지
