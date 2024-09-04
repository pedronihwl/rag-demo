import axios, { AxiosInstance, AxiosRequestConfig, AxiosResponse } from "axios";

export const getCookie = (cookieName: string) => {
    const name = `${cookieName}=`;
    const decodedCookie = decodeURIComponent(document.cookie);
    const ca = decodedCookie.split(";");
    for (let i = 0; i < ca.length; i += 1) {
        let c = ca[i];
        while (c.charAt(0) === " ") {
            c = c.substring(1);
        }
        if (c.indexOf(name) === 0) {
            return c.substring(name.length, c.length);
        }
    }
    return "";
};

export class HttpClient {

    private readonly axiosInstance: AxiosInstance;

    constructor() {
        this.axiosInstance = axios.create();
        this.axiosInstance.interceptors.request.use((config) => {
            config.headers["X-XSRF-TOKEN"] = getCookie("XSRF-RequestToken");
            return config;
        });
    }

    async request<T>(config: AxiosRequestConfig): Promise<T> {
        const params: AxiosRequestConfig = {
            ...config,
            url: `${HttpClient.getCurrentHost()}/api${config.url}`,
        };

        const response: AxiosResponse<T> = await this.axiosInstance.request<T>(params);
        return response.data;
    }

    public static getCurrentHost() {
        const host = window.location.host;
        const url = `${window.location.protocol}//${host}`;
        return url;
    }
}