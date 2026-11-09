// Cloudflare Pages 방식으로 올릴 때 쓰는 입구 (Workers 방식은 src/worker.js)
import { handleApi } from '../../src/api.js';

export const onRequest = ({ request, env, params }) =>
  handleApi(request, env, (params.route || []).join('/'));
