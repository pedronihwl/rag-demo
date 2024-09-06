import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Copy, Upload } from "@phosphor-icons/react";
import { useRef } from "react";
import { useParams } from "react-router-dom";
import FileContainer from "./components/FileContainer";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useApi } from "@/services/useApi";
import { Answer, DbContext, DbFile } from "@/types/@types";
import Content from "./components/Content";
import Viewer from "./components/Viewer";
import { handleFiles } from "@/components/functions/handleFiles";


const Chat = () => {
    const client = useQueryClient()
    const { api } = useApi()
    const { id } = useParams()
    const inputRef = useRef<HTMLInputElement>(null);

    const { mutate: _deleteFile } = useMutation({
        mutationFn: async (fileId: string) => {
            await api.request({
                url: `/files/${fileId}`,
                method: 'DELETE',
                params: {
                    context: id
                }
            })
        },
        onSuccess: async () => {
            await client.invalidateQueries({ queryKey: ['context', id] })
        }
    })

    const { mutate: _addFile } = useMutation({
        mutationFn: async (file: Partial<DbFile>) => {
            const formData = new FormData()

            if (file.hash && file.stream) {
                formData.append(file.hash, file.stream)
            }

            await api.request({
                url: `/context/${id}/files`,
                method: 'POST',
                params: {
                    context: id
                },
                data: formData
            })
        },
        onSuccess: async () => {
            await client.invalidateQueries({ queryKey: ['context', id] })
        }
    })

    const { data: blob, mutate } = useMutation({
        mutationFn: async ({ contextId, references }: { contextId: string; references: string[] }) => {

            const response = await api.request<any>({
                url: `/fragments/${contextId}`,
                params: {
                    fonts: references.join(',')
                },
                responseType: 'blob'
            })

            const blob = new Blob([response], { type: 'application/pdf' });

            return blob;
        }
    }
    );

    const { data, isError} = useQuery({
        queryFn: async () => {
            return api.request<DbContext>({
                url: `/context/${id}`
            })
        },
        refetchInterval: ({ state}) => {
            if(state.status === 'success' && state.data?.files.some(file => file.status === 'Processing')){
                return 3000
            }
            
            return false; 
        },
        queryKey: ['context', id]
    })

    const getFile = (answer: Answer) => {
        mutate({
            contextId: id!,
            references: answer.fonts
        })
    }

    const deleteFile = (file: DbFile) => {
        _deleteFile(file.id)
    }

    const addFile = (e: React.ChangeEvent<HTMLInputElement>) => {
        if (e.target.files && e.target.files[0]) {
            handleFiles(e.target.files, files => {
                const dbFile = (files as Partial<DbFile>[])[0]
                _addFile(dbFile)
            })
        }
    }

    const onExplorer = (e: React.MouseEvent<HTMLButtonElement, MouseEvent>) => {
        e.preventDefault();
        inputRef.current?.click();
    }

    if (!data) return <div>Carregando...</div>

    if (isError) return <div>404</div>

    const chatDisabled = data.files.length === 0 || data.files.some(item => item.status !== 'Processed')

    return <div className="flex w-full h-full">
        <input accept=".pdf" ref={inputRef} style={{ display: "none" }} type="file" multiple={false} onChange={addFile}></input>
        <aside className="p-4 h-full min-w-80">
            <Badge variant="secondary" className="cursor-pointer w-full flex justify-between py-2">{data.id}<Copy size={18} className="ml-2" /></Badge>
            <div>
                <Button variant="secondary" className="w-full my-4" onClick={onExplorer}>
                    <Upload size={32} className="mr-2 h-4 w-4" /> Novo documento
                </Button>

                <div className="flex flex-col gap-4 mt-2">
                    {data.files.map(item => (<FileContainer file={item} key={item.name} onDelete={deleteFile} />))}
                </div>
            </div>
        </aside>
        <section className="flex w-full gap-4">
            <div className="flex-1">
                {blob && <Viewer blob={blob} />}
            </div>
            <div className="flex-1">
                <Content cb={getFile} isDisabled={chatDisabled} />
            </div>
        </section>

    </div>

}

export default Chat;