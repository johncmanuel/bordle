import { defineConfig } from 'vite';

// https://vitejs.dev/config/
export default defineConfig({
	envDir: '../../',
	server: {
		port: 3000,
		proxy: {
			'/api': {
				target: 'http://localhost:5229',
				changeOrigin: true,
				secure: false,
				ws: true,
			},
		},
	},
});