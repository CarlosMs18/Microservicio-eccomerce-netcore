import http from 'k6/http';
import { check } from 'k6';
import { uuidv4 } from 'https://jslib.k6.io/k6-utils/1.4.0/index.js';

// Configuración
const USER_API_URL = 'https://localhost:7251';
const CATALOG_API_URL = 'https://localhost:7204';
const AUTH_ENDPOINT = '/api/User/login';
const CATEGORY_ENDPOINT = '/api/Category/CreateCategory';

// Lista de usuarios (todos con la misma contraseña)
const TEST_USERS = [
    { email: "c@gmail.com", password: "Ab$12345" },
    { email: "test@gmail.com", password: "Ab$12345" },
    { email: "test1@gmail.com", password: "Ab$12345" },
    { email: "test2@gmail.com", password: "Ab$12345" },
    { email: "test3@gmail.com", password: "Ab$12345" },
    { email: "test4@gmail.com", password: "Ab$12345" },
    { email: "test5@gmail.com", password: "Ab$12345" },
     { email: "test6@gmail.com", password: "Ab$12345" },
    { email: "test7@gmail.com", password: "Ab$12345" },
    { email: "test8@gmail.com", password: "Ab$12345" },
    { email: "test9@gmail.com", password: "Ab$12345" },
    { email: "test10@gmail.com", password: "Ab$12345" },
     { email: "test11@gmail.com", password: "Ab$12345" },
    { email: "test12@gmail.com", password: "Ab$12345" },
    { email: "test13@gmail.com", password: "Ab$12345" },
    { email: "test14@gmail.com", password: "Ab$12345" },
    { email: "test15@gmail.com", password: "Ab$12345" },
     { email: "test16@gmail.com", password: "Ab$12345" },
    { email: "test17@gmail.com", password: "Ab$12345" },
    { email: "test18@gmail.com", password: "Ab$12345" },
    { email: "test19@gmail.com", password: "Ab$12345" },
    { email: "test20@gmail.com", password: "Ab$12345" },
    { email: "test21@gmail.com", password: "Ab$12345" },
    { email: "test22@gmail.com", password: "Ab$12345" },
    { email: "test23@gmail.com", password: "Ab$12345" },
    { email: "test24@gmail.com", password: "Ab$12345" },
    { email: "test25@gmail.com", password: "Ab$12345" },
    { email: "test26@gmail.com", password: "Ab$12345" },
    { email: "test27@gmail.com", password: "Ab$12345" },
    { email: "test28@gmail.com", password: "Ab$12345" },
    { email: "test29@gmail.com", password: "Ab$12345" },
    { email: "test30@gmail.com", password: "Ab$12345" }
];

export const options = {
    scenarios: {
        concurrent_users: {
            executor: 'per-vu-iterations', // Cada VU ejecuta su propia iteración
            vus: TEST_USERS.length,        // Número de usuarios = cantidad en TEST_USERS
            iterations: 6,                // 1 iteración por usuario
            maxDuration: '2m'
        }
    },
    thresholds: {
        http_req_duration: ['p(95)<500'], // 95% de las peticiones <500ms
        http_req_failed: ['rate<0.01']    // Menos del 1% de errores
    }
};

export default function () {
    // Seleccionar usuario según el VU actual
    const user = TEST_USERS[__VU - 1]; // __VU empieza en 1

    // 1. Login - Cada usuario se autentica independientemente
    const loginRes = http.post(
        `${USER_API_URL}${AUTH_ENDPOINT}`,
        JSON.stringify({ email: user.email, password: user.password }),
        {
            headers: { 'Content-Type': 'application/json' },
            insecureSkipTLSVerify: true
        }
    );

    if (!check(loginRes, {
        'Login exitoso (200)': (r) => r.status === 200,
        'Token recibido': (r) => r.json('token') !== null
    })) {
        console.error(`[VU ${__VU}] Falló login para ${user.email}`);
        return;
    }

    const authToken = loginRes.json('token');

    // 2. Crear categoría con nombre único
    const uniqueName = `Cat_${uuidv4()}_${__VU}_${__ITER}`;
    const categoryPayload = JSON.stringify({
        Request: {
            name: uniqueName,
            description: "Load test"
        }
    });

    const createRes = http.post(
        `${CATALOG_API_URL}${CATEGORY_ENDPOINT}`,
        categoryPayload,
        {
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${authToken}`
            },
            insecureSkipTLSVerify: true
        }
    );

    // 3. Verificaciones
    const checks = check(createRes, {
        'Status 201 (Created)': (r) => r.status === 201,
        'ID de categoría generado': (r) => r.json('id') !== null
    });

    if (!checks) {
        console.error(`[VU ${__VU}] Error en creación: ${createRes.status} - ${createRes.body}`);
    } else {
        console.log(`[VU ${__VU}] Categoría creada: ${uniqueName}`);
    }
}