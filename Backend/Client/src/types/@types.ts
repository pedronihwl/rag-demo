
export type Map<K extends string | number | symbol, V> = {
    [key in K]: V;
};

export type DbFileStatus = 'PROCESSED' | 'NOT_PROCESSED' | 'PROCESSING'

export type DbFile = {
    id: string;
    context: string;
    status: DbFileStatus;
    name: string;
    pages: number;
    processedPages: number;
    url: string;
    stream?: File,
    mime?: string,
    size?: number,
    hash?: string;
}

export type DbFragment = {
    id: string;
    context: string;
    file: string;
    index: number;  /* Página que foi extraída o fragmento */
    offset: number; /* Tamanho do fragmento */

}

export type DbContext = {
    id: string;
    createdAt: string;
    files: DbFile[];
}