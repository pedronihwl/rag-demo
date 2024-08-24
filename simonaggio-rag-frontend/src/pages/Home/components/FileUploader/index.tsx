import { Button } from "@/components/ui/button";
import { FilePdf, Trash, UploadSimple } from "@phosphor-icons/react";
import clsx from "clsx";
import { useRef, useState } from "react";


type IProps = {
    files: any[]
    action: React.Dispatch<React.SetStateAction<any[]>>
}

const FileUploader = ({ files, action }: IProps) => {
    const inputRef = useRef<HTMLInputElement>(null);
    const [dragActive, setDragActive] = useState(false);

    const handleFiles = (fileList: FileList) => {
        const auxFiles = [];

        for (let i = 0; i < fileList.length; i++) {
            const file = fileList[i];

            if (file.type !== 'application/pdf') {
                continue
            }

            const fileName = file.name
                .replace(/[^\w\s.]/gi, "")
                .replace(/\s+/g, "_")
                .toLowerCase();

            auxFiles.push({
                hash: fileName,
                stream: file,
                mime: file.type,
                size: (file.size / (1024 * 1024)),
                id: i
            });
        }

        action(auxFiles)
    };

    const handleChange = (
        e: React.DragEvent<HTMLDivElement> | React.ChangeEvent<HTMLInputElement>
    ) => {
        e.preventDefault();
        e.stopPropagation();

        if ("dataTransfer" in e) {
            if (e.type === "dragenter" || e.type === "dragover") {
                setDragActive(true);
            } else if (e.type === "dragleave") {
                setDragActive(false);
            } else if (e.type === "drop") {
                setDragActive(false);
                if (e.dataTransfer.files && e.dataTransfer.files[0]) {
                    handleFiles(e.dataTransfer.files);
                }
            }
        } else if (e.target.files && e.target.files[0]) {
            handleFiles(e.target.files);
        }
    };

    const onExplorer = (e: React.MouseEvent<HTMLButtonElement, MouseEvent>) => {
        e.preventDefault();
        inputRef.current?.click();
    }

    const onDelete = (index: number) => {
        action(files.filter(item => item.id !== index))
    }

    return <div>
        <div onDragEnter={handleChange}
            onDragOver={handleChange}
            onDragLeave={handleChange}
            onDrop={handleChange} className={clsx(
                'flex flex-col items-center justify-center rounded-md p-4 border',
                {
                    'border-slate-500 border-dashed': !dragActive,
                    'border-green-600 border-solid': dragActive
                }
            )}>
            <UploadSimple color={`${dragActive ? '#16a34a' : '#020617'}`} size={42} weight="light" />
            <input accept=".pdf" ref={inputRef} style={{ display: "none" }} type="file" multiple={true} onChange={handleChange}></input>
            <p className="text-sm text-muted-foreground">Arraste os arquivos ou <Button variant="link" onClick={onExplorer} className="text-green-600 p-0">insira manualmente</Button> </p>
        </div>
        {files.map(item => (
            <div key={item.id} className="rounded shadow p-2 border border-slate-200 flex items-center justify-between my-4">
                <div className="flex">
                    <FilePdf size={42} weight="light" color="#7f1d1d" className="mr-4" />
                    <div>
                        <p className="leading-7">{item.hash}</p>
                        <p className="text-sm text-muted-foreground text-slate-500">.pdf {item.size.toFixed(4)} MB</p>
                    </div>
                </div>
                <Trash size={28} onClick={() => onDelete(item.id)} weight="light" className="hover:cursor-pointer hover:text-red-900" />
            </div>))}
    </div>

}

export default FileUploader;