import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from "@/components/ui/card";
import { useState } from "react";
import FileUploader from "./components/FileUploader";
import { Label } from "@/components/ui/label";
import { Input } from "@/components/ui/input";


const Home = () => {
    const [files, setFiles] = useState<any[]>([])
    const [toggle, setToggle] = useState(true)

    return <div className="flex items-center justify-center w-full h-full">
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
                    {files.length > 0 && <Button className="w-full">Enviar arquivos e criar contexto</Button>}
                </CardFooter>
            </> : <>
                <CardFooter className="flex items-center space-y-6">
                    <div>
                        <Label htmlFor="contextId">ID do Contexto</Label>
                        <Input className="w-[280px] outline-none" id="contextId" />
                    </div>
                    <Button type="submit" className="ml-4 w-full">Buscar</Button>
                </CardFooter>
            </>}
        </Card>
    </div>

}

export default Home;