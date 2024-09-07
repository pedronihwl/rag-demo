import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from "@/components/ui/card";
import { useState } from "react";
import FileUploader from "./components/FileUploader";
import { Label } from "@/components/ui/label";
import { Input } from "@/components/ui/input";
import { useMutation } from "@tanstack/react-query";
import { useApi } from "@/services/useApi";
import { DbContext, DbFile } from "@/types/@types";
import { useNavigate } from "react-router-dom";

import { Loader2, Terminal } from "lucide-react"
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";


const Home = () => {
    const nav = useNavigate()
    const { api } = useApi()

    const { mutate: findContext, isPending: isLoading } = useMutation({
        mutationFn: async (id: string) => {
            const response = await api.request<DbContext>({
                url: `/context/${id}`
            })

            return response.id;
        }
    })

    const { mutate, isPending } = useMutation({
        mutationFn: async (files: Partial<DbFile>[]) => {
            const formData = new FormData()

            files.forEach(file => {
                if (file.hash && file.stream) {
                    formData.append(file.hash, file.stream)
                }
            })

            const response = await api.request<DbContext>({
                url: '/context',
                method: 'POST',
                data: formData
            })

            return response
        }
    })

    const [files, setFiles] = useState<any[]>([])
    const [value, setValue] = useState('')
    const [toggle, setToggle] = useState(true)

    const onPostContext = () => {
        if (files.length == 0) {
            return;
        }

        mutate(files, {
            onSuccess: (data) => {
                nav(`/context/${data.id}`)
            }
        })
    }

    const onGetContext = () => {
        if (!value) return;
        findContext(value, {
            onSuccess: (data) => {
                nav(`/context/${data}`)
            }
        })
    }

    return <div className="flex items-center justify-center w-full h-full">
        <div className="w-[550px]">
        <Alert className="mb-4" variant="destructive">
            <Terminal className="h-4 w-4" />
            <AlertTitle>Projeto em fase inicial de desenvolvimento</AlertTitle>
            <AlertDescription>
                Esse projeto está em fase inicial de desenvolvimento e, portanto, é recomendado apenas para fins de teste e aprendizado. Não é indicado para uso em casos reais ou produção.
            </AlertDescription>
        </Alert>

        <Card>
            <CardHeader>
                <CardTitle>Contexto</CardTitle>
                <CardDescription>Faça o upload dos arquivos para criar um novo contexto, ou acesse um existente</CardDescription>
                <Button variant="link" onClick={() => setToggle(!toggle)} className="w-fit px-0">{toggle ? 'Buscar contexto existente' : 'Criar novo contexto'}</Button>
            </CardHeader>
            {toggle ? <>
                <CardContent>
                    <FileUploader action={setFiles} files={files} />
                </CardContent>
                <CardFooter>
                    {files.length > 0 && <Button disabled={isPending} className="w-full" onClick={onPostContext}>
                        {isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                        Enviar arquivos e criar contexto
                    </Button>}
                </CardFooter>
            </> : <>
                <CardFooter className="flex items-center space-y-6">
                    <div>
                        <Label htmlFor="contextId">ID do Contexto</Label>
                        <Input className="w-[280px] outline-none" id="contextId" value={value} onChange={e => setValue(e.target.value)} />
                    </div>
                    <Button type="button" className="ml-4 w-full" onClick={onGetContext}>
                        {isLoading && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                        Buscar
                    </Button>
                </CardFooter>
            </>}
        </Card>
        <p className="mt-4 text-sm text-muted-foreground text-white text-center">Copyright ©2024 • Todos os direitos reservados para Simonaggio</p>
        </div>
    </div>
}

export default Home;