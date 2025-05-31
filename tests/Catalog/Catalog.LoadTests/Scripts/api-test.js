import http from 'k6/http';
import { check } from 'k6';
import { uuidv4 } from 'https://jslib.k6.io/k6-utils/1.4.0/index.js';

// Configuración
const USER_API_URL = 'http://user-service.dev.svc.cluster.local'; 
const CATALOG_API_URL = 'http://catalog-service.dev.svc.cluster.local';
const AUTH_ENDPOINT = '/api/User/login';
const CATEGORY_ENDPOINT = '/api/Category/CreateCategory';

// Lista de usuarios (todos con la misma contraseña)
const TEST_USERS = [
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
        warmup: {
            executor: 'constant-vus',
            vus: 5,  // 5 VUs para precalentar
            duration: '30s',
        },
        main_test: {
            executor: 'constant-vus',
            vus: TEST_USERS.length, // 31 VUs (igual al tamaño de TEST_USERS)
            duration: '1m',
            startTime: '30s', // Inicia después del warmup
        },
    },
    thresholds: {
        http_req_duration: ['p(95)<700', 'p(99)<1000'], // Más estricto
        http_req_failed: ['rate<0.01'],
        checks: ['rate>0.99'], // 99% de checks deben pasar
    },
    noConnectionReuse: false, // Reutiliza conexiones HTTP (mejor rendimiento)
};

export default function () {
    // Seleccionar usuario usando módulo para evitar errores
    const userIndex = (__VU - 1) % TEST_USERS.length;
    const user = TEST_USERS[userIndex];

    // Validación adicional para evitar undefined
    if (!user) {
        console.error(`[VU ${__VU}] Usuario no definido en el índice ${userIndex}`);
        return;
    }

    // 1. Login (con manejo de errores robusto)
    let loginRes;
    try {
        loginRes = http.post(
            `${USER_API_URL}${AUTH_ENDPOINT}`,
            JSON.stringify({ email: user.email, password: user.password }),
            {
                headers: { 'Content-Type': 'application/json' },
                timeout: '30s', // Timeout específico
            }
        );

        if (!check(loginRes, {
            'Login exitoso (200)': (r) => r.status === 200,
            'Token recibido': (r) => r.json('token') !== null,
        })) {
            console.error(`[VU ${__VU}] Login fallido: ${loginRes.status} - ${loginRes.body}`);
            return;
        }
    } catch (e) {
        console.error(`[VU ${__VU}] Error en login: ${e}`);
        return;
    }

    const authToken = loginRes.json('token');

    // 2. Crear categoría (con reintentos opcionales)
    const uniqueName = `Cat_${uuidv4()}_${__VU}_${__ITER}`;
    const payload = JSON.stringify({
        Request: {
            name: uniqueName,
            description: "Load test",
        },
    });

    let createRes;
    try {
        createRes = http.post(
            `${CATALOG_API_URL}${CATEGORY_ENDPOINT}`,
            payload,
            {
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${authToken}`,
                },
                timeout: '30s',
            }
        );

        check(createRes, {
            'Status 201 (Created)': (r) => r.status === 201,
            'ID de categoría generado': (r) => r.json('id') !== null,
        }) || console.error(`[VU ${__VU}] Error en creación: ${createRes.status} - ${createRes.body}`);
    } catch (e) {
        console.error(`[VU ${__VU}] Error en creación: ${e}`);
    }
}