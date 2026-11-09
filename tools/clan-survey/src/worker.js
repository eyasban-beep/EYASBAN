// Cloudflare Workers 입구: /api/* 는 서버 코드, 나머지는 public/ 화면 파일
import { handleApi } from './api.js';

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    if (url.pathname.startsWith('/api/')) return handleApi(request, env, url.pathname.slice(5));
    return env.ASSETS.fetch(request);
  },
};
